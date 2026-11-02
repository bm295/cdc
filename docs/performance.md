# Document QA latency, throughput and cost

Run from the repository root:

```bash
python -m document_qa.benchmark --requests 40 --concurrency 4 --warmup 3
# Optional infrastructure estimate using your own USD/hour rate:
python -m document_qa.benchmark --infrastructure-hourly-usd 0.10
```

The command starts an isolated local HTTP server, loads the evaluation corpus into
a temporary disk-backed SQLite database, and exercises authenticated POST /api/ask.
It does not mutate the user's database or need model credentials. Questions cycle
through the evaluation dataset. Defaults are top_k=3 and real authorization/rate
limits; warmup consumes the same subject's 60/minute request allowance. A larger
run can legitimately produce 429s. Do not interpret fast rejections as useful capacity.

JSON output defaults to artifacts/document-qa-benchmark.json. Samples contain request
index, status and elapsed latency only: no credentials, questions or answer content.
CLI exits nonzero if any measured request fails. Warmup failure also aborts the run.

| Field | Meaning |
| --- | --- |
| http_latency_success.p50_ms / p95_ms | Nearest-rank percentile of HTTP 200 request durations |
| http_latency_all | Includes errors and transport failures; separate from success percentiles |
| successful_requests_per_second | HTTP 200 completions divided by measured wall-clock seconds |
| completed_requests_per_second | All attempts completed, including failures, divided by wall time |
| status_counts / error_rate | HTTP status and transport_error counts; failures / attempts |
| inference_cost_usd | Zero for current extractive engine; no model calls/tokens occur |
| estimated_infrastructure_window_usd | Measured seconds / 3600 × configured USD/hour; null if unspecified |
| estimated_infrastructure_per_success_usd | Window infrastructure estimate / successful responses; null if undefined |

Latency is measured client-side from starting an HTTP request through reading its
entire response. It includes connection establishment, server scheduling, auth,
policy locking, SQLite audit, retrieval, serialization and loopback transport.
It excludes executor queue wait before the worker starts the request. Throughput
uses the full measured batch wall time, including worker scheduling, but excludes
warmup, corpus loading, server startup and shutdown. Each request opens a connection.
This is a closed-loop fixed-concurrency benchmark, not an arrival-rate load generator.
The answer's existing latency_ms is narrower retrieval/answer time and should not
be compared directly to the HTTP percentile.

Only HTTP 200 defines success here; use the separate evaluation gate for semantic
quality. No token estimator or provider price is invented. Zero inference cost does
not mean free hosting: the optional infrastructure estimate excludes idle time,
storage, network and other unconfigured charges. A future model adapter must record
actual usage and versioned provider rates before claiming measured inference cost.

CI uploads a small performance smoke report without machine-dependent latency
thresholds. It is not a sustained capacity test or SLO. Compare runs with identical
corpus, top-k, concurrency, storage and hardware; small samples give unstable tails.
For production capacity measurement use a representative corpus and controlled
load duration, include rate-limit policy and monitor resource saturation.
