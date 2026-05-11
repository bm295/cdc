# Debezium CDC Demo (C#)

This repository is focused on Change Data Capture (CDC) using Debezium and includes a **C# Kafka consumer**.

## Stack

- MySQL (source database)
- Kafka + Zookeeper
- Debezium Connect
- Kafka UI
- C# consumer (`Confluent.Kafka`)

## Run

```bash
docker compose up -d --build
```

## Register Debezium Connector

```bash
curl -X POST http://localhost:8083/connectors \
  -H 'Content-Type: application/json' \
  -d @connectors/mysql-inventory.json
```

Verify connector status:

```bash
curl http://localhost:8083/connectors/inventory-connector/status
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

Observe CDC events in:

- consumer logs (`docker logs -f consumer`)
- Kafka UI at http://localhost:8080

## Tear down

```bash
docker compose down -v
```
