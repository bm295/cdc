# Debezium CDC Demo (C#)

## Reusable CDC library

`src/Cdc.Extensions` contains a packable .NET 8 library with typed topic subscriptions,
scoped handlers, Debezium JSON parsing, retries, manual offset commits and dead letters.
The `consumer` app references this library and supplies the customer model and MySQL replica handler.
See [Cdc.Extensions usage and packaging](src/Cdc.Extensions/README.md).

This repository demonstrates a local Change Data Capture pipeline:

```text
MySQL -> Debezium Connect -> Kafka -> C# worker consumer
```

## Stack

- MySQL source database
- Kafka + Zookeeper
- Debezium Connect
- Kafka UI
- C# worker service using `Confluent.Kafka`
- Replica DB table (`inventory.customers_replica`) updated by the worker
- Demo web app for triggering and observing the full flow

## How CDC Works

See [docs/cdc-flow.md](docs/cdc-flow.md) for the end-to-end flow, component responsibilities, event shape, retry behavior, offset commits, and DLQ path.

See [docs/architecture.md](docs/architecture.md) for architecture characteristics, decisions, style, and logical components.

See [docs/demo-web.md](docs/demo-web.md) for the interactive browser demo.

## Run

```bash
docker compose -f deploy/docker-compose.yml up -d --build
```

The Compose setup waits for Kafka/MySQL readiness, bootstraps `inventory.customers_replica` from the current source table, and registers the Debezium connector through the `connector-init` service.

Open the demo web app:

```text
http://localhost:5173
```

Verify connector status:

```bash
curl http://localhost:8083/connectors/inventory-connector/status
```

If you want to register or update the connector manually from PowerShell:

```powershell
./deploy/scripts/register-connectors.ps1
```

## Generate CDC Events

```bash
docker exec -it mysql mysql -uroot -pdebezium
```

```sql
USE inventory;

INSERT INTO customers(first_name, last_name, email)
VALUES ('John', 'Doe', 'john.doe@example.com');

UPDATE customers
SET email = 'john.new@example.com'
WHERE first_name = 'John' AND last_name = 'Doe';

DELETE FROM customers
WHERE first_name = 'John' AND last_name = 'Doe';
```

Observe CDC events and replicated rows in:

- demo web app: http://localhost:5173
- consumer logs: `docker logs -f consumer`
- Kafka UI: http://localhost:8080
- dead-letter topic: `cdc.dead-letter`
- replica table check: `SELECT * FROM inventory.customers_replica;`

## Test

```bash
dotnet test cdc.sln
```

## Tear Down

```bash
docker compose -f deploy/docker-compose.yml down -v
```

## Document QA with evaluation

A runnable local document question-answering project is available in
[`document_qa`](document_qa). It includes a web UI, HTTP API, persistent SQLite
storage, overlapping chunks, BM25 retrieval, exact source citations, abstention,
and an offline evaluation quality gate in CI. This version uses extractive
answers and requires no model API key. See [setup, API and evaluation guide](docs/document-qa.md).

```bash
python -m document_qa.ingest README.md docs/cdc-flow.md docs/architecture.md docs/demo-web.md
python -m document_qa.server
# Open http://127.0.0.1:8090
python -m document_qa.evaluate
```

## Decision records and runtime sequences

See [Architecture decision records](docs/adr/README.md) and
[sequence diagrams](docs/sequence-diagrams.md) for CDC delivery, scoped dispatch,
local document QA, tool contracts and action security, including failure paths
and current limitations.

## CI/CD and integration tests

See [pipeline, container testing and deployment guide](docs/ci-cd.md).

## Performance measurements

See [HTTP p50/p95, throughput and cost benchmark](docs/performance.md).
