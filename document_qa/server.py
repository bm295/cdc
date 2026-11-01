"""Local HTTP API; no external dependencies or remote model calls."""
import argparse
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote
from .core import KnowledgeBase


def handler_for(kb):
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
            elif self.path == '/api/documents':
                self.send(200, kb.documents())
            else:
                self.send(404, {'error': 'Not found'})

        def do_POST(self):
            if self.path not in ('/api/documents', '/api/ask', '/api/search'):
                return self.send(404, {'error': 'Not found'})
            try:
                size = int(self.headers.get('Content-Length', '0'))
                if not 0 < size <= 1_100_000:
                    return self.send(413, {'error': 'Body must contain 1..1100000 bytes'})
                body = json.loads(self.rfile.read(size))
                if not isinstance(body, dict):
                    raise ValueError('Expected a JSON object')
                if self.path == '/api/documents':
                    result = kb.ingest(body['id'], body['title'], body['text'])
                else:
                    method = kb.ask if self.path == '/api/ask' else kb.search
                    result = method(body['question'], body.get('top_k', 3))
                self.send(200, result)
            except (ValueError, KeyError, TypeError, UnicodeError) as exc:
                self.send(400, {'error': str(exc)})

        def do_DELETE(self):
            prefix = '/api/documents/'
            if not self.path.startswith(prefix):
                return self.send(404, {'error': 'Not found'})
            deleted = kb.delete(unquote(self.path[len(prefix):]))
            self.send(200 if deleted else 404, {'deleted': deleted})
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
