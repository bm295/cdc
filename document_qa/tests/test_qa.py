import http.client
import json
import tempfile
import threading
import unittest
from pathlib import Path
from http.server import ThreadingHTTPServer
from document_qa.core import KnowledgeBase
from document_qa.evaluate import evaluate, score
from document_qa.server import handler_for
from document_qa.security import ExecutionContext


class RetrievalTests(unittest.TestCase):
    def setUp(self):
        self.kb = KnowledgeBase(':memory:', chunk_size=6, overlap=2)

    def tearDown(self):
        self.kb.db.close()

    def test_chunk_overlap_and_citation(self):
        self.kb.ingest('a', 'Title', 'one two three four five six seven eight nine')
        result = self.kb.ask('seven', 1)
        citation = result['citations'][0]
        self.assertEqual(citation['word_offset'], 4)
        self.assertEqual(citation['quote'], 'five six seven eight nine')
        self.assertEqual(result['answer'], '[1] five six seven eight nine')

    def test_upsert_removes_stale_chunks_and_delete(self):
        self.kb.ingest('a', 'old', 'obsolete stale')
        self.kb.ingest('a', 'new', 'current evidence')
        self.assertEqual(self.kb.search('obsolete'), [])
        self.assertEqual(len(self.kb.documents()), 1)
        self.assertTrue(self.kb.delete('a'))
        self.assertFalse(self.kb.delete('a'))
        self.assertEqual(self.kb.search('current'), [])

    def test_persistence(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = str(Path(tmp) / 'kb.db')
            kb = KnowledgeBase(path)
            kb.ingest('a', 'Vietnamese', 'Thanh toán bằng tiền mặt')
            kb.db.close()
            kb = KnowledgeBase(path)
            self.assertEqual(kb.search('TIỀN MẶT')[0]['document_id'], 'a')
            kb.db.close()

    def test_abstention_and_validation(self):
        self.assertTrue(self.kb.ask('unknown')['abstained'])
        self.kb.ingest('a', 'a', 'Kafka streams')
        self.assertTrue(self.kb.ask('dinosaurs')['abstained'])
        for k in (0, 21, True, '3'):
            with self.assertRaises(ValueError):
                self.kb.ask('Kafka', k)
        with self.assertRaises(ValueError):
            self.kb.ask(' ')
        with self.assertRaises(ValueError):
            self.kb.ingest('', 'a', 'b')

    def test_stable_ids(self):
        self.kb.ingest('a', 'title', 'same content')
        before = self.kb.search('same')[0]['chunk_id']
        self.kb.ingest('a', 'title', 'same content')
        self.assertEqual(before, self.kb.search('same')[0]['chunk_id'])

    def test_evaluation_math_and_regression(self):
        case = {'relevant_documents': ['a','b'], 'reference_answer':'alpha', 'should_abstain':False}
        result = {'citations':[{'document_id':'x','chunk_id':'x','label':1,'quote':'alpha'},
                               {'document_id':'a','chunk_id':'a','label':2,'quote':'alpha'}],
                  'answer':'invented answer', 'abstained':False, 'latency_ms':1}
        metrics = score(case, result)
        self.assertEqual(metrics['recall_at_k'], .5)
        self.assertEqual(metrics['precision_at_k'], .5)
        self.assertEqual(metrics['mrr'], .5)
        self.assertEqual(metrics['extractive_integrity'], 0)
        root = Path(__file__).resolve().parents[2]
        dataset = json.loads((root/'document_qa/data/evaluation.json').read_text())
        self.assertTrue(evaluate(dataset, root)['passed'])
        dataset['thresholds']['mrr'] = 1.1
        self.assertFalse(evaluate(dataset, root)['passed'])


class ApiTests(unittest.TestCase):
    def setUp(self):
        self.kb = KnowledgeBase(':memory:')
        self.server = ThreadingHTTPServer(('127.0.0.1', 0), handler_for(self.kb, {'test-token': ExecutionContext('test', frozenset({'documents:read','documents:write','documents:delete'}), None)}))
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join()
        self.kb.db.close()

    def request(self, method, path, body=None):
        conn = http.client.HTTPConnection('127.0.0.1', self.server.server_port)
        headers = {'Authorization': 'Bearer test-token'}
        if (path == '/api/documents' and method == 'POST' and isinstance(body, dict) and set(body) == {'id','title','text'}) or (method == 'DELETE' and path.startswith('/api/documents/')):
            name = 'upsert_document' if method == 'POST' else 'delete_document'
            arguments = body if method == 'POST' else {'id': path[len('/api/documents/'): ]}
            status, pending = self.request('POST', '/api/tools/prepare', {'name': name, 'arguments': arguments})
            if status == 200:
                headers['X-Confirmation-Token'] = json.loads(pending)['confirmation_token']
        conn.request(method, path, json.dumps(body) if body is not None else None, headers)
        response = conn.getresponse()
        data = response.read()
        conn.close()
        return response.status, data

    def test_full_lifecycle(self):
        self.assertEqual(self.request('GET', '/')[0], 200)
        self.assertEqual(self.request('GET', '/health')[0], 200)
        self.assertEqual(self.request('POST', '/api/documents', {'id':'a','title':'Runbook','text':'Kafka retries failed messages.'})[0], 200)
        status, body = self.request('POST', '/api/ask', {'question':'Kafka retries'})
        self.assertEqual(status, 200)
        self.assertEqual(json.loads(body)['citations'][0]['document_id'], 'a')
        self.assertEqual(len(json.loads(self.request('GET', '/api/documents')[1])), 1)
        self.assertEqual(self.request('DELETE', '/api/documents/a')[0], 200)
        self.assertTrue(json.loads(self.request('POST', '/api/ask', {'question':'Kafka'})[1])['abstained'])

    def test_errors(self):
        for body in ({}, [], {'question':' '}, {'question':'a','top_k':False}):
            self.assertEqual(self.request('POST', '/api/ask', body)[0], 400)
        self.assertEqual(self.request('GET', '/missing')[0], 404)
        self.assertEqual(self.request('DELETE', '/missing')[0], 404)
        self.assertEqual(self.request('POST', '/api/ask')[0], 413)


if __name__ == '__main__':
    unittest.main()
