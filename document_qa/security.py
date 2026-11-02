"""Server-owned identities, authorization, bounded confirmations and audit trail."""
import hashlib
import hmac
import json
import secrets
import threading
import time
from dataclasses import dataclass


@dataclass(frozen=True)
class ExecutionContext:
    subject: str
    scopes: frozenset
    # None grants all documents; an empty set grants none.
    document_ids: frozenset | None = frozenset()


class Policy:
    def __init__(self, kb, requests_per_minute=60, max_documents=1000,
                 max_characters=10_000_000, confirmation_ttl=120, clock=time.monotonic):
        self.kb = kb
        self.limit, self.max_documents, self.max_characters = requests_per_minute, max_documents, max_characters
        self.ttl, self.clock = confirmation_ttl, clock
        self.lock = threading.RLock()
        self.pending, self.rates = {}, {}
        kb.db.execute('CREATE TABLE IF NOT EXISTS tool_audit (timestamp REAL, subject TEXT, tool TEXT, outcome TEXT)')

    def fail(self, code):
        from .tools import ToolError
        raise ToolError(code, code.replace('_', ' '))

    def authorize(self, ctx, name, args):
        if not isinstance(ctx, ExecutionContext) or not ctx.subject:
            self.fail('unauthenticated')
        scope = {'upsert_document': 'documents:write', 'delete_document': 'documents:delete'}.get(name, 'documents:read')
        if scope not in ctx.scopes:
            self.fail('forbidden')
        if 'id' in args and ctx.document_ids is not None and args['id'] not in ctx.document_ids:
            self.fail('forbidden')

    def rate(self, ctx):
        now = self.clock()
        self.rates = {k: v for k, v in self.rates.items() if v[0] + 60 > now}
        start, count = self.rates.get(ctx.subject, (now, 0))
        if count >= self.limit:
            self.fail('rate_limited')
        self.rates[ctx.subject] = (start, count + 1)

    def binding(self, ctx, name, args):
        digest = hashlib.sha256(json.dumps(args, sort_keys=True, ensure_ascii=False, allow_nan=False).encode()).hexdigest()
        return ctx.subject, name, digest

    def prepare(self, ctx, name, args):
        self.authorize(ctx, name, args)
        self.rate(ctx)
        if name not in ('upsert_document', 'delete_document'):
            self.fail('confirmation_not_required')
        now = self.clock()
        self.pending = {k: v for k, v in self.pending.items() if v[1] > now}
        if len(self.pending) >= 1000:
            self.fail('confirmation_capacity_exceeded')
        token = secrets.token_urlsafe(32)
        self.pending[token] = (self.binding(ctx, name, args), now + self.ttl)
        return {'confirmation_token': token, 'expires_in_seconds': self.ttl,
                'action': name, 'document_id': args['id']}

    def consume(self, ctx, name, args, token):
        if not isinstance(token, str):
            self.fail('confirmation_required')
        record = self.pending.get(token)
        if record is None or record[1] <= self.clock() or record[0] != self.binding(ctx, name, args):
            self.fail('invalid_confirmation')
        del self.pending[token]

    def quota(self, args):
        count, chars = self.kb.db.execute('SELECT count(*), coalesce(sum(length(content)),0) FROM documents').fetchone()
        old = self.kb.db.execute('SELECT length(content) FROM documents WHERE id=?', (args['id'],)).fetchone()
        if count + (old is None) > self.max_documents or chars - (old[0] if old else 0) + len(args['text']) > self.max_characters:
            self.fail('quota_exceeded')

    def audit(self, ctx, name, outcome):
        # Never persist arguments, document text, bearer credentials or confirmation tokens.
        with self.kb.db:
            self.kb.db.execute('INSERT INTO tool_audit VALUES(?,?,?,?)',
                              (time.time(), ctx.subject if isinstance(ctx, ExecutionContext) else 'anonymous', str(name)[:100], outcome))


def authenticate(header, identities):
    """Compare opaque bearer credentials to trusted, server-configured identities."""
    if not header or not header.startswith('Bearer '):
        return None
    supplied = header[7:]
    for token, context in identities.items():
        if hmac.compare_digest(supplied.encode(), token.encode()):
            return context
    return None
