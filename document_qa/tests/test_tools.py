import json
import unittest
from unittest.mock import Mock
from document_qa.core import KnowledgeBase
from document_qa.tools import ToolRegistry, ToolError, validate
from document_qa.tests import test_qa


class ToolTests(unittest.TestCase):
    def setUp(self):
        self.kb = KnowledgeBase(':memory:')
        self.tools = ToolRegistry(self.kb)
        self.addCleanup(self.kb.db.close)

    def test_contracts_and_lifecycle(self):
        definitions = self.tools.definitions()
        self.assertEqual(len(definitions), 5)
        definitions[0]['input_schema']['properties'].clear()
        self.tools.invoke('upsert_document', {'id': 'a', 'title': 'Runbook', 'text': 'Kafka retries'})
        cases = [('list_documents', {}), ('search_documents', {'question': 'Kafka'}),
                 ('answer_question', {'question': 'Kafka', 'top_k': 1}),
                 ('answer_question', {'question': 'unknown'}), ('delete_document', {'id': 'a'}),
                 ('delete_document', {'id': 'a'})]
        schemas = {d['name']: d['output_schema'] for d in self.tools.definitions()}
        for name, args in cases:
            validate(self.tools.invoke(name, args), schemas[name])

    def test_invalid_arguments_cannot_mutate(self):
        for args in ({'id': 'a', 'title': 'a', 'text': 'a', 'extra': 1},
                     {'id': 'a', 'title': 'a'}, {'id': 'a', 'title': ' ', 'text': 'a'}):
            with self.assertRaises(ToolError) as ctx:
                self.tools.invoke('upsert_document', args)
            self.assertEqual(ctx.exception.code, 'invalid_arguments')
        self.assertEqual(self.kb.documents(), [])
        for args in (None, [], {}, {'question': 'a', 'top_k': True},
                     {'question': 'a', 'top_k': 21}, {'question': 'a', 'typo': 3}):
            with self.assertRaises(ToolError):
                self.tools.invoke('search_documents', args)
        with self.assertRaises(ToolError) as ctx:
            self.tools.invoke('__dict__', {})
        self.assertEqual(ctx.exception.code, 'unknown_tool')

    def test_output_and_execution_errors_are_sanitized(self):
        for method in (Mock(return_value=[{'invalid': True}]), Mock(side_effect=RuntimeError('secret'))):
            kb = Mock()
            kb.search = method
            with self.assertRaises(ToolError) as ctx:
                ToolRegistry(kb).invoke('search_documents', {'question': 'a'})
            self.assertEqual(ctx.exception.code, 'tool_execution_failed')
            self.assertNotIn('secret', ctx.exception.message)


class ToolApiTests(test_qa.ApiTests):
    def test_discovery_and_invocation(self):
        status, body = self.request('GET', '/api/tools')
        self.assertEqual(status, 200)
        self.assertEqual(len(json.loads(body)), 5)
        status, body = self.request('POST', '/api/tools/invoke', {'name': 'answer_question', 'arguments': {'question': 'unknown'}})
        self.assertEqual(status, 200)
        self.assertTrue(json.loads(body)['abstained'])
        status, body = self.request('POST', '/api/tools/invoke', {'name': 'list_documents', 'arguments': {'extra': 1}})
        self.assertEqual(status, 400)
        self.assertEqual(json.loads(body)['error']['code'], 'invalid_arguments')
