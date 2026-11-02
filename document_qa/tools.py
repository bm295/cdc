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
    def __init__(self, kb):
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
                               'mutates': spec[3], 'input_schema': dict(spec[1], **{'$schema': 'https://json-schema.org/draft/2020-12/schema'}),
                               'output_schema': dict(spec[2], **{'$schema': 'https://json-schema.org/draft/2020-12/schema'})}
                              for name, spec in self._tools.items()])

    def invoke(self, name, arguments):
        if not isinstance(name, str) or name not in self._tools:
            raise ToolError('unknown_tool', 'Unknown tool name')
        _, input_schema, output_schema, _, execute = self._tools[name]
        try:
            validate(arguments, input_schema)
        except ValueError as exc:
            raise ToolError('invalid_arguments', str(exc)) from exc
        try:
            result = execute(arguments)
            validate(result, output_schema)
        except Exception as exc:
            # Do not expose document content, SQL, or internal exception messages.
            raise ToolError('tool_execution_failed', 'Tool failed to produce a valid result') from exc
        return result
