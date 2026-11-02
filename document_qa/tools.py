"""Versioned tool contracts and dispatch, independent of HTTP/model providers."""
import copy
import math


def obj(properties, required=None):
    return {'type': 'object', 'properties': properties,
            'required': list(properties) if required is None else required,
            'additionalProperties': False}


def string(limit=None):
    schema = {'type': 'string', 'minLength': 1, 'pattern': r'\S'}
    if limit is not None:
        schema['maxLength'] = limit
    return schema


def array(items):
    return {'type': 'array', 'items': items}


ID = string(200)
QUERY = obj({'question': string(4000), 'top_k': {'type': 'integer', 'minimum': 1, 'maximum': 20, 'default': 3}}, ['question'])
HIT = obj({'chunk_id': string(), 'document_id': ID, 'title': string(500),
           'word_offset': {'type': 'integer', 'minimum': 0}, 'quote': string(),
           'score': {'type': 'number', 'minimum': 0}})
CITATION = obj(dict(HIT['properties'], label={'type': 'integer', 'minimum': 1}))
ANSWER = obj({'question': string(4000), 'mode': {'type': 'string', 'enum': ['extractive']},
              'abstained': {'type': 'boolean'}, 'answer': string(), 'citations': array(CITATION),
              'latency_ms': {'type': 'number', 'minimum': 0}})


class ToolError(Exception):
    def __init__(self, code, message):
        self.code, self.message = code, message
        super().__init__(message)

    def as_dict(self):
        return {'code': self.code, 'message': self.message}


def validate(value, schema, path='$'):
    """Validate the JSON Schema keywords used by this module (not a general validator)."""
    import re
    kind = schema['type']
    valid = {'object': lambda: type(value) is dict, 'array': lambda: type(value) is list,
             'string': lambda: type(value) is str, 'integer': lambda: type(value) is int,
             'number': lambda: type(value) in (int, float) and math.isfinite(value),
             'boolean': lambda: type(value) is bool}[kind]()
    if not valid:
        raise ValueError(f'{path}: expected {kind}')
    if 'enum' in schema and value not in schema['enum']:
        raise ValueError(f'{path}: unsupported value')
    if kind == 'object':
        missing = set(schema['required']) - value.keys()
        extra = value.keys() - schema['properties'].keys()
        if missing or extra:
            raise ValueError(f'{path}: missing {sorted(missing)}, unknown {sorted(extra)}')
        for key, item in value.items():
            validate(item, schema['properties'][key], f'{path}.{key}')
    elif kind == 'array':
        for index, item in enumerate(value):
            validate(item, schema['items'], f'{path}[{index}]')
    elif kind == 'string':
        if len(value) < schema.get('minLength', 0) or len(value) > schema.get('maxLength', math.inf):
            raise ValueError(f'{path}: string length out of range')
        if 'pattern' in schema and not re.search(schema['pattern'], value):
            raise ValueError(f'{path}: must contain non-whitespace characters')
    elif kind in ('integer', 'number'):
        if value < schema.get('minimum', -math.inf) or value > schema.get('maximum', math.inf):
            raise ValueError(f'{path}: number out of range')


class ToolRegistry:
    def __init__(self, kb, policy=None):
        from .security import Policy
        self.kb = kb
        self.policy = policy if policy is not None else Policy(kb)
        self._tools = {
            'search_documents': ('Search indexed passages; scores are BM25, not confidence.', QUERY, array(HIT), False,
                                 lambda a: kb.search(a['question'], a.get('top_k', 3))),
            'answer_question': ('Return extractive answers with citations; abstain when no passages match.', QUERY, ANSWER, False,
                                lambda a: kb.ask(a['question'], a.get('top_k', 3))),
            'list_documents': ('List document identifiers and titles.', obj({}), array(obj({'id': ID, 'title': string(500)})), False,
                               lambda a: kb.documents()),
            'upsert_document': ('Create or replace a document and its chunks. Reusing an id overwrites content.',
                                obj({'id': ID, 'title': string(500), 'text': string(1_000_000)}),
                                obj({'id': ID, 'chunks': {'type': 'integer', 'minimum': 1}}), True,
                                lambda a: kb.ingest(a['id'], a['title'], a['text'])),
            'delete_document': ('Delete a document and its chunks; deleted=false when absent.', obj({'id': ID}),
                                obj({'deleted': {'type': 'boolean'}}), True, lambda a: {'deleted': kb.delete(a['id'])}),
        }

    def definitions(self):
        return copy.deepcopy([{'name': name, 'description': spec[0], 'version': '1.0',
                               'mutates': spec[3], 'requires_confirmation': spec[3],
                               'required_scope': {'upsert_document': 'documents:write', 'delete_document': 'documents:delete'}.get(name, 'documents:read'),
                               'input_schema': dict(spec[1], **{'$schema': 'https://json-schema.org/draft/2020-12/schema'}),
                               'output_schema': dict(spec[2], **{'$schema': 'https://json-schema.org/draft/2020-12/schema'})}
                              for name, spec in self._tools.items()])

    def _spec(self, name, arguments):
        if not isinstance(name, str) or name not in self._tools:
            raise ToolError('unknown_tool', 'Unknown tool name')
        spec = self._tools[name]
        try:
            validate(arguments, spec[1])
        except ValueError as exc:
            raise ToolError('invalid_arguments', str(exc)) from exc
        return spec

    def prepare(self, name, arguments, context=None):
        with self.policy.lock, self.kb.lock:
            try:
                self._spec(name, arguments)
                result = self.policy.prepare(context, name, arguments)
                self.policy.audit(context, name, 'prepared')
                return result
            except ToolError as exc:
                self.policy.audit(context, name, exc.code)
                raise

    def invoke(self, name, arguments, context=None, confirmation_token=None):
        # Serialize policy checks and writes, including token consumption and quotas.
        with self.policy.lock, self.kb.lock:
            try:
                spec = self._spec(name, arguments)
                self.policy.authorize(context, name, arguments)
                self.policy.rate(context)
                if spec[3]:
                    if name == 'upsert_document':
                        self.policy.quota(arguments)
                    self.policy.consume(context, name, arguments, confirmation_token)
                if name == 'list_documents':
                    result = self.kb.documents(context.document_ids)
                elif name in ('search_documents', 'answer_question'):
                    method = self.kb.search if name == 'search_documents' else self.kb.ask
                    result = method(arguments['question'], arguments.get('top_k', 3), context.document_ids)
                else:
                    result = spec[4](arguments)
                validate(result, spec[2])
                self.policy.audit(context, name, 'succeeded')
                return result
            except ToolError as exc:
                self.policy.audit(context, name, exc.code)
                raise
            except Exception as exc:
                self.policy.audit(context, name, 'tool_execution_failed')
                raise ToolError('tool_execution_failed', 'Tool failed to produce a valid result') from exc
