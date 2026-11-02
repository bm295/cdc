# ADR 0002: Typed topic subscriptions and a scope per processing attempt

- Status: Accepted
- Documented: 2026-11-02

## Context

Kafka transport, envelope parsing and customer persistence change for different
reasons. Supporting more tables should not require embedding table logic in the loop.

## Decision

Keep reusable contracts, parsing, registration and Kafka infrastructure in
Cdc.Extensions. Route the raw ConsumedMessage by its exact topic in ChangeDispatcher.
Create an asynchronous dependency-injection scope for each dispatch attempt.
The selected typed subscription parses the envelope and resolves its registered
handler; tombstones complete without invoking the handler. CustomerChangeHandler
owns operation semantics and delegates replica writes to MySqlReplicaCustomerStore.
An unregistered topic fails dispatch and follows the normal retry/DLQ path.

## Alternatives

- One central switch over table names: fewer abstractions but couples transport to table logic.
- A separate consumer implementation per table: duplicates retry and offset behavior.
- One singleton handler: simpler lifetimes but unsafe for scoped dependencies.

## Consequences

A new table needs a model, handler and topic registration. Failed attempts create
fresh scopes, not fresh durable event identities. Scoped lifetimes improve resource
isolation but do not make repeated side effects idempotent. Topic registration is
static; this is not a dynamic plugin loader or wildcard router.

## Evidence and revisit criteria

See [dispatcher](../../src/Cdc.Extensions/Application/ChangeDispatcher.cs),
[subscription](../../src/Cdc.Extensions/Application/ChangeSubscription.cs) and
[registration tests](../../tests/Cdc.Extensions.Tests/RegistrationTests.cs).
Revisit for multiple handlers per topic or schema-version routing. The
[dispatch sequence](../sequence-diagrams.md#cdc-dispatch-and-projection) shows parsing in its actual location.
