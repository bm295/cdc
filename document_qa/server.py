"""Local HTTP API; no external dependencies or remote model calls."""
import argparse
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote
from .core import KnowledgeBase
from .tools import ToolRegistry, ToolError


def handler_for(kb):
    registry = ToolRegistry(kb)
    class Handler(BaseHTTPRequestHandler):
        def send(self, status, value, content_type='application/json; charset=utf-8'):
            data = value.encode() if isinstance(value, str) else json.dumps(value, ensure_ascii=False).encode()
            self.send_response(status)
            self.send_header('Content-Type', content_type)
            self.send_header('Content-Length', str(len(data)))
            self.send_header('X-Content-Type-Options', 'nosniff')
            self.end_headers()
            self.wfile.write(data)

        def do_GET(self):
            if self.path == '/':
                self.send(200, Path(__file__).with_name('index.html').read_text(), 'text/html; charset=utf-8')
            elif self.path == '/health':
                self.send(200, {'status': 'ok'})
            elif self.path == '/api/tools':
                self.send(200, registry.definitions())
            elif self.path == '/api/documents':
                self.send(200, registry.invoke('list_documents', {}))
            else:
                self.send(404, {'error': 'Not found'})

        def do_POST(self):
            if self.path not in ('/api/documents', '/api/ask', '/api/search', '/api/tools/invoke'):
                return self.send(404, {'error': 'Not found'})
            try:
                size = int(self.headers.get('Content-Length', '0'))
                if not 0 < size <= 1_100_000:
                    return self.send(413, {'error': 'Body must contain 1..1100000 bytes'})
                body = json.loads(self.rfile.read(size))
                if not isinstance(body, dict):
                    raise ValueError('Expected a JSON object')
                if self.path == '/api/tools/invoke':
                    # Arguments are validated against the selected tool's own schema.
                    if set(body) != {'name', 'arguments'}:
                        raise ToolError('invalid_arguments', 'Expected name and arguments')
                    result = registry.invoke(body['name'], body['arguments'])
                else:
                    name = {'/api/documents': 'upsert_document', '/api/ask': 'answer_question',
                            '/api/search': 'search_documents'}[self.path]
                    result = registry.invoke(name, body)
                self.send(200, result)
            except ToolError as exc:
                self.send(500 if exc.code == 'tool_execution_failed' else 400, {'error': exc.as_dict()})
            except (ValueError, KeyError, TypeError, UnicodeError) as exc:
                self.send(400, {'error': str(exc)})

        def do_DELETE(self):
            prefix = '/api/documents/'
            if not self.path.startswith(prefix):
                return self.send(404, {'error': 'Not found'})
            try:
                result = registry.invoke('delete_document', {'id': unquote(self.path[len(prefix):])})
                self.send(200 if result['deleted'] else 404, result)
            except ToolError as exc:
                self.send(500 if exc.code == 'tool_execution_failed' else 400, {'error': exc.as_dict()})
    return Handler


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--db', default='knowledge.db')
    parser.add_argument('--port', type=int, default=8090)
    args = parser.parse_args()
    server = ThreadingHTTPServer(('127.0.0.1', args.port), handler_for(KnowledgeBase(args.db)))
    print(f'Document QA: http://127.0.0.1:{args.port}', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == '__main__':
    main()
