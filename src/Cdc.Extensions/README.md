# Cdc.Extensions

.NET 8 library for consuming typed Debezium JSON events from Kafka.
Debezium/Kafka Connect must be deployed and configured separately.

## Register

```csharp
using Cdc.Extensions;
using Cdc.Extensions.Application;

builder.Services.AddCdc(builder.Configuration.GetSection("Cdc"))
    .Subscribe<Customer, CustomerHandler>("mysql.inventory.customers")
    .Subscribe<Order, OrderHandler>("mysql.inventory.orders");
```

```json
{
  "Cdc": {
    "BootstrapServers": "localhost:9092",
    "GroupId": "my-service-cdc",
    "RetryDelaySeconds": 3,
    "MaxProcessingAttempts": 3,
    "EnableDeadLetterTopic": true,
    "DeadLetterTopic": "my-service.cdc.dead-letter"
  }
}
```

```csharp
public sealed class CustomerHandler(AppDbContext db) : IChangeHandler<Customer>
{
    public async Task HandleAsync(ChangeEvent<Customer> change, CancellationToken ct)
    {
        // Apply Create/Update/Delete/Read (snapshot)/Truncate to your domain.
        // Use Before for deletes and After for inserts, updates and snapshots.
        await db.SaveChangesAsync(ct);
    }
}
```

Handlers are registered as scoped. Each processing attempt creates and asynchronously
disposes a scope, allowing scoped dependencies such as DbContext. Register those dependencies
in the application's service collection. One handler is allowed per exact topic; call AddCdc once.
Broker, consumer group and at least one subscription are required and validated at startup.
Use JSON property attributes for database names such as `first_name`.

## Processing semantics

Events are processed sequentially. Only after successful handling or successful dead-letter
publication is the offset committed. Commit failures restart the consumer without treating
the event as a handler failure. Cancellation does not publish a dead letter or commit.
Handler failures retry with a fresh scope, then go to the configured DLQ. With DLQ disabled,
an exhausted failure restarts consumption without committing the failed event.

Delivery is at-least-once: replay is possible after a crash, rebalance or commit failure.
Handlers must tolerate duplicates (for example idempotent upserts, or transactional deduplication
using topic/partition/offset). Ordering is guaranteed only within a Kafka partition.
Long handler/retry durations must stay within Kafka's poll interval; this MVP uses the
client defaults and does not expose authentication, custom consumer configuration or parallel processing.

Supports JSON envelopes with and without `payload`, c/u/d/r/t operations and null Kafka
values as tombstones (ignored). Avro, Protobuf, transformed flat events and automatic
Debezium logical-type conversion are outside this version's scope.

## Package

From the repository root:

```sh
dotnet pack src/Cdc.Extensions/Cdc.Extensions.csproj -c Release -o artifacts/packages
```

Publish the `.nupkg` to your team's NuGet feed, or add `artifacts/packages` as a local
NuGet source, then add `Cdc.Extensions` version `0.1.0` to the consuming repository.
