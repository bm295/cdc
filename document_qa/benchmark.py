"""HTTP benchmark for local extractive QA; no provider/model billing is incurred."""
import argparse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import http.client
import json
import math
from pathlib import Path
import platform
import secrets
import tempfile
import threading
import time
from http.server import ThreadingHTTPServer
from .core import KnowledgeBase
from .security import ExecutionContext
from .server import handler_for


def percentile(values, fraction):
    """Nearest-rank percentile; empty populations are undefined."""
    if not values:
        return None
    ordered = sorted(values)
    return ordered[max(0, math.ceil(fraction * len(ordered)) - 1)]


def summarize(rows, elapsed_seconds, hourly_cost_usd=None):
    if elapsed_seconds <= 0 or (hourly_cost_usd is not None and (hourly_cost_usd < 0 or not math.isfinite(hourly_cost_usd))):
        raise ValueError('Require positive elapsed time and finite nonnegative hourly cost')
    successes = [r for r in rows if r['status'] == 200]
    def latencies(population):
        values = [r['latency_ms'] for r in population]
        return {'count': len(values), 'p50_ms': percentile(values, .50),
                'p95_ms': percentile(values, .95)}
    infra = elapsed_seconds / 3600 * hourly_cost_usd if hourly_cost_usd is not None else None
    return {'requests': len(rows), 'succeeded': len(successes), 'failed': len(rows)-len(successes),
            'status_counts': dict(Counter(str(r['status']) for r in rows)),
            'elapsed_seconds': elapsed_seconds,
            'completed_requests_per_second': len(rows)/elapsed_seconds,
            'successful_requests_per_second': len(successes)/elapsed_seconds,
            'error_rate': (len(rows)-len(successes))/len(rows) if rows else None,
            'http_latency_all': latencies(rows), 'http_latency_success': latencies(successes),
            'cost': {'currency': 'USD', 'inference_cost_usd': 0,
                     'inference_cost_per_success_usd': 0 if successes else None,
                     'model_calls': 0, 'input_tokens': 0, 'output_tokens': 0,
                     'basis': 'Extractive BM25; no model inference or provider call',
                     'configured_infrastructure_hourly_usd': hourly_cost_usd,
                     'estimated_infrastructure_window_usd': infra,
                     'estimated_infrastructure_per_success_usd': infra/len(successes) if successes and infra is not None else None}}


def benchmark(requests=40, concurrency=4, warmup=3, top_k=3, hourly_cost_usd=None):
    if not 1 <= requests <= 100000 or not 1 <= concurrency <= 128 or not 0 <= warmup <= 1000 or not 1 <= top_k <= 20:
        raise ValueError('Invalid requests, concurrency, warmup or top_k')
    root = Path(__file__).resolve().parents[1]
    dataset = json.loads((root/'document_qa/data/evaluation.json').read_text())
    questions = [case['question'] for case in dataset['cases']]
    with tempfile.TemporaryDirectory() as tmp:
        kb = KnowledgeBase(str(Path(tmp)/'benchmark.db'))
        server = None
        thread = None
        try:
            for doc in dataset['documents']:
                kb.ingest(doc['id'], doc['title'], (root/doc['path']).read_text(encoding='utf-8'))
            credential = secrets.token_urlsafe(32)
            context = ExecutionContext('benchmark', frozenset({'documents:read'}), None)
            # Preserve the real policy/rate limits; denied requests are measured as failures.
            server = ThreadingHTTPServer(('127.0.0.1', 0), handler_for(kb, {credential:context}))
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            def request(index):
                started = time.perf_counter()
                conn = http.client.HTTPConnection('127.0.0.1',server.server_port,timeout=10)
                status = 'transport_error'
                try:
                    conn.request('POST','/api/ask',json.dumps({'question':questions[index % len(questions)],'top_k':top_k}),
                                 {'Authorization':'Bearer '+credential,'Content-Type':'application/json'})
                    response = conn.getresponse()
                    response.read()
                    status = response.status
                except (OSError, http.client.HTTPException):
                    pass
                finally:
                    conn.close()
                return {'request_index':index, 'status':status,
                        'latency_ms':(time.perf_counter()-started)*1000}
            for index in range(warmup):
                result = request(index)
                if result['status'] != 200:
                    raise RuntimeError('Warmup request failed')
            with ThreadPoolExecutor(max_workers=concurrency) as pool:
                started = time.perf_counter()
                rows = list(pool.map(request,range(requests)))
                elapsed = time.perf_counter()-started
            return {'schema_version':'1.0', 'timestamp_utc':datetime.now(timezone.utc).isoformat(),
                    'environment': {'python':platform.python_version(), 'platform':platform.system()},
                    'configuration': {'requests':requests, 'concurrency':concurrency, 'warmup':warmup,
                                      'top_k':top_k, 'documents':len(dataset['documents']),
                                      'storage':'temporary disk SQLite', 'endpoint':'POST /api/ask',
                                      'rate_limit_requests_per_minute':60},
                    'summary':summarize(rows,elapsed,hourly_cost_usd), 'samples':rows}
        finally:
            if server:
                server.shutdown()
                server.server_close()
            if thread:
                thread.join()
            kb.db.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--requests',type=int,default=40)
    parser.add_argument('--concurrency',type=int,default=4)
    parser.add_argument('--warmup',type=int,default=3)
    parser.add_argument('--top-k',type=int,default=3)
    parser.add_argument('--infrastructure-hourly-usd',type=float)
    parser.add_argument('--output',type=Path,default=Path('artifacts/document-qa-benchmark.json'))
    args = parser.parse_args()
    if args.infrastructure_hourly_usd is not None and (not math.isfinite(args.infrastructure_hourly_usd) or args.infrastructure_hourly_usd < 0):
        parser.error('Infrastructure cost must be finite and nonnegative')
    try:
        result = benchmark(args.requests,args.concurrency,args.warmup,args.top_k,args.infrastructure_hourly_usd)
    except ValueError as exc:
        parser.error(str(exc))
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result['summary'],indent=2))
    return 1 if result['summary']['failed'] else 0


if __name__ == '__main__':
    raise SystemExit(main())
