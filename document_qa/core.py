"""Persistent document retrieval and grounded extractive question answering."""
import hashlib
import math
import re
import sqlite3
import threading
import time
from collections import Counter


def tokens(text):
    return re.findall(r"[^\W_]+", text.casefold(), re.UNICODE)


class KnowledgeBase:
    def __init__(self, path='knowledge.db', chunk_size=180, overlap=30):
        if not 0 <= overlap < chunk_size:
            raise ValueError('Require 0 <= overlap < chunk_size')
        self.size, self.overlap = chunk_size, overlap
        self.lock = threading.RLock()
        self.db = sqlite3.connect(path, check_same_thread=False)
        self.db.executescript('''
            CREATE TABLE IF NOT EXISTS documents(id TEXT PRIMARY KEY, title TEXT NOT NULL, content TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS chunks(id TEXT PRIMARY KEY, document_id TEXT NOT NULL, position INTEGER, text TEXT);
        ''')

    def ingest(self, document_id, title, text):
        if not all(isinstance(x, str) and x.strip() for x in (document_id, title, text)):
            raise ValueError('id, title and text must be nonempty strings')
        if len(text) > 1_000_000 or len(document_id) > 200 or len(title) > 500:
            raise ValueError('Document exceeds limits')
        words = text.split()
        chunks = []
        for start in range(0, len(words), self.size - self.overlap):
            part = ' '.join(words[start:start + self.size])
            digest = hashlib.sha256(f'{document_id}:{start}:{part}'.encode()).hexdigest()[:24]
            chunks.append((digest, document_id, start, part))
            if start + self.size >= len(words):
                break
        with self.lock, self.db:
            self.db.execute('INSERT OR REPLACE INTO documents VALUES(?,?,?)', (document_id, title, text))
            self.db.execute('DELETE FROM chunks WHERE document_id=?', (document_id,))
            self.db.executemany('INSERT INTO chunks VALUES(?,?,?,?)', chunks)
        return {'id': document_id, 'chunks': len(chunks)}

    def documents(self):
        with self.lock:
            return [dict(zip(('id', 'title'), row)) for row in self.db.execute('SELECT id,title FROM documents ORDER BY id')]

    def delete(self, document_id):
        with self.lock, self.db:
            self.db.execute('DELETE FROM chunks WHERE document_id=?', (document_id,))
            return self.db.execute('DELETE FROM documents WHERE id=?', (document_id,)).rowcount > 0

    def search(self, question, top_k=3):
        if not isinstance(question, str) or not question.strip() or len(question) > 4000:
            raise ValueError('Question must contain 1..4000 characters')
        if type(top_k) is not int or not 1 <= top_k <= 20:
            raise ValueError('top_k must be an integer from 1 to 20')
        with self.lock:
            rows = self.db.execute('SELECT c.id,c.document_id,d.title,c.position,c.text FROM chunks c JOIN documents d ON d.id=c.document_id ORDER BY c.id').fetchall()
        if not rows:
            return []
        counts = [Counter(tokens(row[4])) for row in rows]
        average = sum(sum(c.values()) for c in counts) / len(counts)
        query = set(tokens(question))
        frequency = {t: sum(t in c for c in counts) for t in query}
        results = []
        for row, count in zip(rows, counts):
            length = sum(count.values())
            score = sum(math.log(1 + (len(rows)-frequency[t]+.5)/(frequency[t]+.5)) * count[t]*2.2 /
                        (count[t]+1.2*(.25+.75*length/max(average, 1))) for t in query if count[t])
            if score > 0:
                results.append(dict(zip(('chunk_id', 'document_id', 'title', 'word_offset', 'quote'), row), score=score))
        return sorted(results, key=lambda r: (-r['score'], r['chunk_id']))[:top_k]

    def ask(self, question, top_k=3):
        start = time.perf_counter()
        hits = self.search(question, top_k)
        return {'question': question, 'mode': 'extractive', 'abstained': not hits,
                'answer': '\n\n'.join(f"[{i}] {h['quote']}" for i, h in enumerate(hits, 1)) if hits else 'Không tìm thấy bằng chứng trong tài liệu.',
                'citations': [dict(h, label=i) for i, h in enumerate(hits, 1)],
                'latency_ms': round((time.perf_counter()-start)*1000, 3)}
