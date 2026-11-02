import http.client
import json
import threading
import unittest
from http.server import ThreadingHTTPServer

from document_qa.core import KnowledgeBase
from document_qa.monitoring import Monitoring, MonitoredProvider, parse_rate
from document_qa.security import ExecutionContext
from document_qa.server import handler_for
from document_qa.tools import ToolError


class MonitoringTests(unittest.TestCase):
    def setUp(self):
        self.kb = KnowledgeBase(':memory:')
        self.addCleanup(self.kb.db.close)
        self.monitor = Monitoring(self.kb, 2, 4)
        self.admin = ExecutionContext('ops', frozenset({'monitoring:read'}))
        self.reader = ExecutionContext('reader', frozenset({'documents:read'}))

    def test_cost_errors_and_unknown_usage(self):
        self.monitor.record('model', 'success', 10, {'input_tokens':1000, 'output_tokens':500})
        self.monitor.record('model', 'error', 20, None)
        self.monitor.record('model', 'success', 30, {'input_tokens':0, 'output_tokens':0})
        data = self.monitor.snapshot(self.admin)['models'][0]
        self.assertEqual((data['calls'], data['errors'], data['unpriced_calls']), (3, 1, 1))
        self.assertAlmostEqual(data['estimated_usd'], .004)
        self.assertEqual(data['average_latency_ms'], 20)
        with self.assertRaises(ToolError):
            self.monitor.snapshot(self.reader)
        self.assertNotIn('prompt', str(self.kb.db.execute('SELECT * FROM ai_usage').fetchall()))

    def test_provider_failures_are_recorded(self):
        class Broken:
            model = 'broken'
            def create(self, inputs):
                raise RuntimeError('secret')
        with self.assertRaises(RuntimeError):
            MonitoredProvider(Broken(), self.monitor).create(['private prompt'])
        self.assertEqual(self.monitor.snapshot(self.admin)['models'][0]['errors'], 1)
        self.assertNotIn('secret', str(self.kb.db.execute('SELECT * FROM ai_usage').fetchall()))

    def test_rates_validate(self):
        self.assertEqual(parse_rate('0'), 0)
        self.assertIsNone(parse_rate(''))
        for value in ('-1', 'nan', 'inf', 'invalid'):
            with self.assertRaises(ValueError):
                parse_rate(value)

    def test_http_requires_monitoring_scope(self):
        server = ThreadingHTTPServer(('127.0.0.1', 0), handler_for(self.kb, {'admin':self.admin, 'reader':self.reader}))
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            for token, expected in ((None, 401), ('reader', 403), ('admin', 200)):
                conn = http.client.HTTPConnection('127.0.0.1', server.server_port)
                conn.request('GET', '/api/monitoring', headers={'Authorization':'Bearer ' + token} if token else {})
                response = conn.getresponse()
                self.assertEqual(response.status, expected)
                json.loads(response.read())
                conn.close()
        finally:
            server.shutdown()
            server.server_close()
            thread.join()
