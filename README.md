# Debezium CDC Demo (C#)

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
