"""Local HTTP API; no external dependencies or remote model calls."""
import argparse
import json
import os
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote
from .core import KnowledgeBase
from .tools import ToolRegistry, ToolError
from .security import ExecutionContext, authenticate


def handler_for(kb, identities=None):
    registry = ToolRegistry(kb)
    identities = identities or {}
    class Handler(BaseHTTPRequestHandler):
        def send(self, status, value, content_type='application/json; charset=utf-8'):
            data = value.encode() if isinstance(value, str) else json.dumps(value, ensure_ascii=False).encode()
            self.send_response(status)
            self.send_header('Content-Type', content_type)
            self.send_header('Content-Length', str(len(data)))
            self.send_header('X-Content-Type-Options', 'nosniff')
            self.end_headers()
            self.wfile.write(data)

        def setup(self):
            super().setup()
            self.connection.settimeout(10)

        def context(self):
            return authenticate(self.headers.get('Authorization'), identities)

        def tool_error(self, exc):
            status = {'unauthenticated': 401, 'forbidden': 403, 'confirmation_required': 409,
                      'invalid_confirmation': 409, 'rate_limited': 429, 'quota_exceeded': 409,
                      'confirmation_capacity_exceeded': 429, 'tool_execution_failed': 500}.get(exc.code, 400)
            self.send(status, {'error': exc.as_dict()})

        def do_GET(self):
            if self.path.startswith('/api/') and self.context() is None:
                return self.tool_error(ToolError('unauthenticated', 'Bearer credential required'))

            if self.path == '/':
                self.send(200, Path(__file__).with_name('index.html').read_text(), 'text/html; charset=utf-8')
            elif self.path == '/health':
                self.send(200, {'status': 'ok'})
            elif self.path == '/api/tools':
                self.send(200, registry.definitions())
            elif self.path == '/api/documents':

                try:
                    self.send(200, registry.invoke('list_documents', {}, self.context()))
                except ToolError as exc:
                    self.tool_error(exc)
            else:
                self.send(404, {'error': 'Not found'})

        def do_POST(self):
            if self.path not in ('/api/documents', '/api/ask', '/api/search', '/api/tools/invoke', '/api/tools/prepare'):
                return self.send(404, {'error': 'Not found'})
            if self.context() is None:
                return self.tool_error(ToolError('unauthenticated', 'Bearer credential required'))
            try:
                size = int(self.headers.get('Content-Length', '0'))
                if not 0 < size <= 1_100_000:
                    return self.send(413, {'error': 'Body must contain 1..1100000 bytes'})
                body = json.loads(self.rfile.read(size))
                if not isinstance(body, dict):
                    raise ValueError('Expected a JSON object')
                if self.path in ('/api/tools/invoke', '/api/tools/prepare'):
                    # Arguments are validated against the selected tool's own schema.
                    if set(body) != {'name', 'arguments'}:
                        raise ToolError('invalid_arguments', 'Expected name and arguments')
                    if self.path.endswith('/prepare'):
                        result = registry.prepare(body['name'], body['arguments'], self.context())
                    else:
                        result = registry.invoke(body['name'], body['arguments'], self.context(), self.headers.get('X-Confirmation-Token'))
                else:
                    name = {'/api/documents': 'upsert_document', '/api/ask': 'answer_question',
                            '/api/search': 'search_documents'}[self.path]
                    result = registry.invoke(name, body, self.context(), self.headers.get('X-Confirmation-Token'))
                self.send(200, result)
            except ToolError as exc:
                self.tool_error(exc)
            except (ValueError, KeyError, TypeError, UnicodeError) as exc:
                self.send(400, {'error': str(exc)})

        def do_DELETE(self):
            prefix = '/api/documents/'
            if not self.path.startswith(prefix):
                return self.send(404, {'error': 'Not found'})
            try:
                result = registry.invoke('delete_document', {'id': unquote(self.path[len(prefix):])}, self.context(), self.headers.get('X-Confirmation-Token'))
                self.send(200 if result['deleted'] else 404, result)
            except ToolError as exc:
                self.tool_error(exc)
    return Handler


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--db', default='knowledge.db')
    parser.add_argument('--port', type=int, default=8090)
    args = parser.parse_args()
    config = os.environ.get('DOCUMENT_QA_IDENTITIES')
    if not config:
        parser.error('Set DOCUMENT_QA_IDENTITIES to a JSON array of trusted bearer identities')
    identities = {}
    for entry in json.loads(config):
        if len(entry['token']) < 32 or entry['token'] in identities or not entry['subject']:
            parser.error('Credentials must be unique, at least 32 characters; subject is required')
        ids = entry.get('document_ids', [])
        identities[entry['token']] = ExecutionContext(entry['subject'], frozenset(entry['scopes']),
                                                      None if ids is None else frozenset(ids))
    server = ThreadingHTTPServer(('127.0.0.1', args.port), handler_for(KnowledgeBase(args.db), identities))
    print(f'Document QA: http://127.0.0.1:{args.port}', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == '__main__':
    main()
