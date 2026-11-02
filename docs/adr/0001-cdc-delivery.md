# ADR 0001: Manual Kafka commits with retries and dead letters

- Status: Accepted
- Documented: 2026-11-02

## Context

The replica must not silently skip a source change when its handler fails. A poison
message must remain inspectable without permanently blocking healthy traffic.

## Decision

Capture MySQL binlog changes through Debezium and buffer them in Kafka. The worker
processes consumed records sequentially within its loop. Store and commit the
offset only after dispatch succeeds or final-attempt failure is successfully
published to the dead-letter topic. Retry dispatch up to MaxProcessingAttempts.
If dead letters are disabled or publication fails, do not reach offset commit;
the outer loop recreates the consumer after a delay. Cancellation also prevents
commit when observed before that step.

## Alternatives

- Automatic commits: simpler, but can acknowledge a record before business work.
- Commit only on handler success forever: retains ordering but a poison record blocks progress.
- Atomic replica write and Kafka acknowledgment: requires coordination not present here.

## Consequences

Delivery is at least once, not exactly once. A crash or commit failure after a
replica write can repeat the handler; commit failure after DLQ publication can
repeat the dead letter. Database writes and offsets are not one transaction.
Ordering is partition-local, not global across consumer instances or topics.
Retries of older changes can overwrite newer projection state in replay scenarios;
there is no durable event deduplication or sequence guard. DLQ replay is an
operator responsibility, not an automated recovery workflow.

## Evidence and revisit criteria

See [KafkaConsumerLoop](../../src/Cdc.Extensions/Infrastructure/Kafka/KafkaConsumerLoop.cs)
and [loop tests](../../tests/Cdc.Extensions.Tests/KafkaConsumerLoopTests.cs).
Revisit when handlers introduce non-repeatable side effects, strict replay ordering,
or stronger delivery guarantees. See [CDC failure sequence](../sequence-diagrams.md#cdc-retries-and-dead-letters).
