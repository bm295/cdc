"""Black-box tests: real process, HTTP and durable SQLite; optional Docker backend."""
import http.client
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import tempfile
import time
import unittest


class ServiceIntegrationTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.token = secrets.token_urlsafe(32)
        self.identities = json.dumps([{'token': self.token, 'subject':'integration',
            'scopes':['documents:read','documents:write','documents:delete'], 'document_ids':None}])
        self.image = os.environ.get('QA_TEST_IMAGE')
        self.name = 'qa-integration-' + secrets.token_hex(6)
        with socket.socket() as sock:
            sock.bind(('127.0.0.1', 0))
            self.port = sock.getsockname()[1]
        self.process = None
        self.addCleanup(self.stop)
        if self.image:
            subprocess.run(['docker','volume','create',self.name], check=True, stdout=subprocess.DEVNULL)
            self.addCleanup(lambda: subprocess.run(['docker','volume','rm',self.name], stdout=subprocess.DEVNULL))
            # Cleanup must remove container before volume.
            self.addCleanup(self.stop)
        self.start()

    def start(self):
        if self.image:
            subprocess.run(['docker','run','-d','--name',self.name,
                '-p',f'127.0.0.1:{self.port}:8090','-e','DOCUMENT_QA_IDENTITIES='+self.identities,
                '-v',self.name+':/data',self.image], check=True, stdout=subprocess.DEVNULL)
        else:
            self.process = subprocess.Popen([sys.executable,'-m','document_qa.server',
                '--port',str(self.port),'--db',str(Path(self.tmp.name)/'knowledge.db')],
                env=dict(os.environ, DOCUMENT_QA_IDENTITIES=self.identities),
                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            try:
                if self.request('GET','/health')[0] == 200:
                    return
            except (OSError, http.client.HTTPException):
                pass
            time.sleep(.1)
        self.fail('Service did not become healthy within 20 seconds')

    def stop(self):
        if self.image:
            subprocess.run(['docker','rm','-f',self.name], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        elif self.process is not None:
            self.process.terminate()
            try:
                self.process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait(timeout=5)
            self.process = None

    def request(self, method, path, body=None, authenticated=True, confirmation=None):
        headers = {'Content-Type':'application/json'}
        if authenticated:
            headers['Authorization'] = 'Bearer ' + self.token
        if confirmation:
            headers['X-Confirmation-Token'] = confirmation
        conn = http.client.HTTPConnection('127.0.0.1', self.port, timeout=2)
        try:
            conn.request(method,path,json.dumps(body) if body is not None else None,headers)
            response = conn.getresponse()
            return response.status,json.loads(response.read())
        finally:
            conn.close()

    def prepare(self, name, args):
        status, value = self.request('POST','/api/tools/prepare',{'name':name,'arguments':args})
        self.assertEqual(status,200,value)
        return value['confirmation_token']

    def test_authenticated_lifecycle_restart_and_audit(self):
        args = {'id':'integration','title':'Runbook','text':'Kafka retries failed messages.'}
        self.assertEqual(self.request('GET','/api/documents',authenticated=False)[0],401)
        self.assertEqual(self.request('POST','/api/documents',args)[0],409)
        token = self.prepare('upsert_document',args)
        self.assertEqual(self.request('POST','/api/documents',args,confirmation=token)[0],200)
        self.assertEqual(self.request('POST','/api/documents',args,confirmation=token)[0],409)
        status, answer = self.request('POST','/api/ask',{'question':'Kafka retries'})
        self.assertEqual(status,200)
        self.assertEqual(answer['citations'][0]['document_id'],'integration')
        pending = self.prepare('delete_document',{'id':'integration'})
        self.stop()
        self.start()
        self.assertEqual(len(self.request('GET','/api/documents')[1]),1)
        self.assertEqual(self.request('DELETE','/api/documents/integration',confirmation=pending)[0],409)
        token = self.prepare('delete_document',{'id':'integration'})
        self.assertEqual(self.request('DELETE','/api/documents/integration',confirmation=token)[0],200)
        self.assertTrue(self.request('POST','/api/ask',{'question':'Kafka'})[1]['abstained'])
        if not self.image:
            import sqlite3
            from contextlib import closing
            with closing(sqlite3.connect(Path(self.tmp.name)/'knowledge.db')) as db:
                outcomes = {r[0] for r in db.execute('SELECT outcome FROM tool_audit')}
            self.assertIn('succeeded', outcomes)
            self.assertIn('invalid_confirmation', outcomes)

    def test_server_refuses_missing_identity_configuration(self):
        if self.image:
            self.skipTest('Startup failure tested by process backend')
        result = subprocess.run([sys.executable,'-m','document_qa.server'],
            env={k:v for k,v in os.environ.items() if k != 'DOCUMENT_QA_IDENTITIES'},
            capture_output=True, timeout=5)
        self.assertNotEqual(result.returncode,0)
        self.assertIn(b'DOCUMENT_QA_IDENTITIES',result.stderr)
