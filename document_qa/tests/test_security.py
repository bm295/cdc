import concurrent.futures
import unittest
from document_qa.core import KnowledgeBase
from document_qa.security import ExecutionContext, Policy, authenticate
from document_qa.tools import ToolRegistry, ToolError


class SecurityTests(unittest.TestCase):
    def setUp(self):
        self.kb = KnowledgeBase(':memory:')
        self.addCleanup(self.kb.db.close)
        self.now = 0
        self.policy = Policy(self.kb, clock=lambda: self.now, max_documents=1, max_characters=20)
        self.registry = ToolRegistry(self.kb, self.policy)
        self.admin = ExecutionContext('admin', frozenset({'documents:read','documents:write','documents:delete'}), None)
        self.reader = ExecutionContext('reader', frozenset({'documents:read'}), frozenset({'a'}))
        self.args = {'id':'a', 'title':'A', 'text':'Kafka evidence'}

    def error(self, code, call):
        with self.assertRaises(ToolError) as ctx:
            call()
        self.assertEqual(ctx.exception.code, code)

    def test_default_deny_and_scopes(self):
        self.error('unauthenticated', lambda: self.registry.invoke('list_documents', {}))
        self.error('forbidden', lambda: self.registry.prepare('upsert_document', self.args, self.reader))
        self.error('confirmation_required', lambda: self.registry.invoke('upsert_document', self.args, self.admin))
        self.assertEqual(self.kb.documents(), [])
        self.assertIsNone(authenticate('Bearer wrong', {'secret':self.admin}))
        self.assertEqual(authenticate('Bearer secret', {'secret':self.admin}), self.admin)

    def test_binding_expiry_replay_and_quota(self):
        token = self.registry.prepare('upsert_document', self.args, self.admin)['confirmation_token']
        changed = dict(self.args, text='changed')
        self.error('invalid_confirmation', lambda: self.registry.invoke('upsert_document', changed, self.admin, token))
        other = ExecutionContext('other', self.admin.scopes, None)
        self.error('invalid_confirmation', lambda: self.registry.invoke('upsert_document', self.args, other, token))
        self.registry.invoke('upsert_document', self.args, self.admin, token)
        self.error('invalid_confirmation', lambda: self.registry.invoke('upsert_document', self.args, self.admin, token))
        token = self.registry.prepare('delete_document', {'id':'a'}, self.admin)['confirmation_token']
        self.now = 121
        self.error('invalid_confirmation', lambda: self.registry.invoke('delete_document', {'id':'a'}, self.admin, token))
        token = self.registry.prepare('upsert_document', dict(self.args, id='b'), self.admin)['confirmation_token']
        self.error('quota_exceeded', lambda: self.registry.invoke('upsert_document', dict(self.args, id='b'), self.admin, token))
        self.assertEqual(len(self.kb.documents()), 1)

    def test_read_isolation_and_audit_redaction(self):
        self.kb.ingest('a','A','Kafka public')
        self.kb.ingest('b','B','Kafka secret')
        self.assertEqual(self.registry.invoke('list_documents', {}, self.reader), [{'id':'a','title':'A'}])
        result = self.registry.invoke('answer_question', {'question':'Kafka'}, self.reader)
        self.assertEqual({c['document_id'] for c in result['citations']}, {'a'})
        self.assertNotIn('secret', result['answer'])
        self.error('forbidden', lambda: self.registry.prepare('delete_document', {'id':'b'}, self.reader))
        audit = str(self.kb.db.execute('SELECT * FROM tool_audit').fetchall())
        self.assertNotIn('Kafka', audit)
        self.assertIn('forbidden', audit)

    def test_rate_and_concurrent_single_use(self):
        token = self.registry.prepare('upsert_document', self.args, self.admin)['confirmation_token']
        def run(_):
            try:
                self.registry.invoke('upsert_document', self.args, self.admin, token)
                return True
            except ToolError:
                return False
        with concurrent.futures.ThreadPoolExecutor(2) as pool:
            self.assertEqual(sum(pool.map(run, range(2))), 1)
        self.policy.limit = 1
        self.error('rate_limited', lambda: self.registry.invoke('list_documents', {}, self.admin))
        self.now = 61
        self.assertEqual(len(self.registry.invoke('list_documents', {}, self.admin)), 1)


class HttpSecurityTests(unittest.TestCase):
    def test_legacy_routes_cannot_bypass_policy(self):
        import http.client
        import json
        import threading
        from http.server import ThreadingHTTPServer
        from document_qa.server import handler_for
        kb = KnowledgeBase(':memory:')
        writer = ExecutionContext('writer', frozenset({'documents:write'}), frozenset({'a'}))
        server = ThreadingHTTPServer(('127.0.0.1', 0), handler_for(kb, {'credential':writer}))
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        def request(method, path, body=None, token=None, confirmation=None):
            connection = http.client.HTTPConnection('127.0.0.1', server.server_port)
            headers = {}
            if token:
                headers['Authorization'] = 'Bearer ' + token
            if confirmation:
                headers['X-Confirmation-Token'] = confirmation
            connection.request(method, path, json.dumps(body) if body is not None else None, headers)
            response = connection.getresponse()
            value = json.loads(response.read())
            connection.close()
            return response.status, value
        try:
            args = {'id':'a','title':'A','text':'Kafka'}
            self.assertEqual(request('POST','/api/documents',args)[0], 401)
            self.assertEqual(request('POST','/api/documents',args,'credential')[0], 409)
            self.assertEqual(request('GET','/api/documents',token='credential')[0], 403)
            self.assertEqual(request('DELETE','/api/documents/a',token='credential')[0], 403)
            status, pending = request('POST','/api/tools/prepare', {'name':'upsert_document','arguments':args}, 'credential')
            self.assertEqual(status, 200)
            confirmation = pending['confirmation_token']
            self.assertEqual(request('POST','/api/documents',args,'credential',confirmation)[0], 200)
            self.assertEqual(request('POST','/api/documents',args,'credential',confirmation)[0], 409)
        finally:
            server.shutdown()
            server.server_close()
            thread.join()
            kb.db.close()
