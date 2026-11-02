"""Persistent aggregate provider telemetry; never stores prompts or responses."""
import math
import time

from .tools import ToolError


class Monitoring:
    def __init__(self, kb, input_rate=None, output_rate=None):
        self.kb, self.input_rate, self.output_rate = kb, input_rate, output_rate
        with kb.lock, kb.db:
            kb.db.execute('''CREATE TABLE IF NOT EXISTS ai_usage (
                timestamp REAL NOT NULL, model TEXT NOT NULL, outcome TEXT NOT NULL,
                latency_ms REAL NOT NULL, input_tokens INTEGER, output_tokens INTEGER,
                estimated_usd REAL)''')
            kb.db.execute('CREATE INDEX IF NOT EXISTS ai_usage_timestamp ON ai_usage(timestamp)')

    def record(self, model, outcome, latency, usage):
        def count(key):
            value = usage.get(key) if isinstance(usage, dict) else None
            return value if type(value) is int and value >= 0 else None
        input_tokens, output_tokens = count('input_tokens'), count('output_tokens')
        cost = None
        if input_tokens is not None and output_tokens is not None and self.input_rate is not None and self.output_rate is not None:
            cost = (input_tokens * self.input_rate + output_tokens * self.output_rate) / 1_000_000
        with self.kb.lock, self.kb.db:
            self.kb.db.execute('DELETE FROM ai_usage WHERE timestamp < ?', (time.time() - 30 * 86400,))
            self.kb.db.execute('INSERT INTO ai_usage VALUES(?,?,?,?,?,?,?)',
                (time.time(), str(model)[:100], outcome, latency, input_tokens, output_tokens, cost))

    def snapshot(self, ctx):
        if 'monitoring:read' not in ctx.scopes:
            raise ToolError('forbidden', 'monitoring access required')
        with self.kb.lock:
            rows = self.kb.db.execute('''SELECT model, count(*),
                sum(CASE WHEN outcome='error' THEN 1 ELSE 0 END), avg(latency_ms),
                sum(input_tokens), sum(output_tokens), sum(estimated_usd),
                sum(CASE WHEN estimated_usd IS NULL THEN 1 ELSE 0 END)
                FROM ai_usage WHERE timestamp >= ? GROUP BY model ORDER BY model''',
                (time.time() - 86400,)).fetchall()
        return {'window_seconds': 86400, 'models': [dict(zip(
            ('model', 'calls', 'errors', 'average_latency_ms', 'input_tokens', 'output_tokens',
             'estimated_usd', 'unpriced_calls'), row)) for row in rows]}


class MonitoredProvider:
    def __init__(self, provider, monitoring):
        self.provider, self.monitoring = provider, monitoring

    def create(self, inputs):
        start = time.monotonic()
        result = None
        outcome = 'error'
        try:
            result = self.provider.create(inputs)
            if isinstance(result, dict) and result.get('status') == 'completed':
                outcome = 'success'
            return result
        finally:
            # Unknown usage is deliberately not counted as zero cost.
            self.monitoring.record(getattr(self.provider, 'model', 'unknown'), outcome,
                (time.monotonic() - start) * 1000, result.get('usage') if isinstance(result, dict) else None)


def parse_rate(value):
    if value is None or value == '':
        return None
    number = float(value)
    if not math.isfinite(number) or number < 0:
        raise ValueError('rates must be finite non-negative USD per million tokens')
    return number
