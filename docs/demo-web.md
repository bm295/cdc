# Demo Web App

The demo web app is an interactive dashboard for observing the CDC pipeline from the browser.

```text
http://localhost:5173
```

It runs as the `demo-web` service in `deploy/docker-compose.yml`.

The Compose startup includes a one-shot `replica-bootstrap` service. It reconciles `inventory.customers_replica` from the current `inventory.customers` table before the consumer starts, so the browser demo begins from a readable baseline even if old Kafka offsets already skipped earlier snapshot messages.

## What It Shows

- The live CDC pipeline from MySQL source rows to the replica table.
- Debezium connector state from Kafka Connect.
- Recent messages from `mysql-server-1.inventory.customers`.
- Recent messages from `cdc.dead-letter`.
- Side-by-side source and replica table contents.
- Architecture details and repo file links for each pipeline stage.

## Actions

The Actions panel can generate CDC traffic:

- `Insert`: inserts a new row into `inventory.customers`.
- `Update`: updates the selected customer id, or the latest source row when no id is provided.
- `Delete`: deletes the selected customer id, or the latest source row when no id is provided.
- `Seed rows`: inserts two sample source rows.
- `Truncate`: truncates `inventory.customers`.
- `Poison event`: publishes a Debezium-shaped message with unsupported `op = x` to the customer topic so the worker retries it and sends it to the DLQ.

## Runtime Flow

1. The browser calls the demo API.
2. The demo API writes source-table changes to MySQL or publishes a poison Kafka message.
3. Debezium captures MySQL changes and writes customer CDC events to Kafka.
4. The C# worker consumes, parses, dispatches, and applies the event to `inventory.customers_replica`.
5. The demo API polls MySQL, Kafka, and Kafka Connect.
6. The browser updates the pipeline, event timeline, topic lists, and table views.

## Backend Endpoints

The web app serves static files and exposes these JSON endpoints:

```text
GET  /api/demo/snapshot
POST /api/demo/actions/insert
POST /api/demo/actions/update
POST /api/demo/actions/delete
POST /api/demo/actions/truncate
POST /api/demo/actions/seed
POST /api/demo/actions/poison
```

## Configuration

Configuration is under the `CdcDemo` section in `demo/DemoWeb/appsettings.json` and is overridden by Compose environment variables:

- `CdcDemo__SourceConnectionString`
- `CdcDemo__ReplicaConnectionString`
- `CdcDemo__KafkaBootstrapServers`
- `CdcDemo__CustomerTopic`
- `CdcDemo__DeadLetterTopic`
- `CdcDemo__KafkaConnectBaseUrl`
- `CdcDemo__ConnectorName`

## Troubleshooting

If Compose cannot publish the demo app port, check whether another local process is already listening on `5173`:

```powershell
Get-NetTCPConnection -LocalPort 5173 -State Listen
```

If the listener is a local `DemoWeb` process, stop it before starting Compose:

```powershell
Stop-Process -Id <process-id>
```

The demo and consumer Docker builds ignore local `bin/` and `obj/` folders. This prevents Windows-generated .NET build assets from being copied into Linux Docker images.

To reset all local CDC data, including Kafka topics, connector offsets, MySQL rows, and DLQ messages:

```bash
docker compose -f deploy/docker-compose.yml down -v
docker compose -f deploy/docker-compose.yml up -d --build
```

If source rows exist but the replica is missing some of them, rerun the README startup command. The `replica-bootstrap` service will reconcile the replica before the consumer continues with new CDC events.
