# Market Readiness Trace

## Repository State

| Classification | Value | Evidence |
| --- | --- | --- |
| DOCUMENTATION_PRESENT | true | `README.md`, `docs/cdc-flow.md`, `docs/architecture.md`, `docs/demo-web.md` |
| BUSINESS_DOCUMENTATION_PRESENT | true | CDC flow documents event operation codes, retry/DLQ behavior, offset commit behavior, and replica update workflow. |
| CODE_READY_REQUIREMENT_PRESENT | true | `docs/cdc-flow.md` defines `op: t` as truncate and `CustomerChangeHandler` identifies the affected code path. |
| TEST_INFRASTRUCTURE_PRESENT | true | `tests/CdcConsumer.Tests` uses xUnit and runs through `dotnet test cdc.sln`. |

## Requirement Traceability

| Requirement ID | Business requirement | Source file/section | Current code evidence | Status | Gap |
| --- | --- | --- | --- | --- | --- |
| CDC-TRUNCATE-001 | A Debezium truncate event for `inventory.customers` must be parsed as a truncate operation so the worker can clear the replica table. | `docs/cdc-flow.md` / Event Flow operation codes and Consumer Processing Flow | `ChangeOperationMapper` maps `t` to `Truncate`; `CustomerChangeHandler` calls `TruncateAsync`. | COVERED | Parser regression test added for `op = t`; handler test added for replica truncation. |
| CDC-CONFIG-001 | Retry and DLQ configuration must reject invalid values so failed-message recovery does not start with unusable retry timing, attempt count, or DLQ routing. | `docs/cdc-flow.md` / Runtime Configuration; `docs/architecture.md` / Recoverability and Commit Offsets After Processing | `KafkaOptions.Validate` checks required Kafka settings, DLQ topic, retry delay, and max attempts. | COVERED | Validation tests added for missing enabled DLQ topic, disabled DLQ topic boundary, non-positive retry delay, and non-positive max attempts. |

## Code Readiness Gate

| Readiness criterion | Met/Not met | Evidence | Missing information or action |
| --- | --- | --- | --- |
| Behavior or business result is specific | Met | `docs/cdc-flow.md` lists truncate as a supported operation and shows handler-to-replica flow. | None |
| Source and section are traceable | Met | `docs/cdc-flow.md` / Event Flow and Consumer Processing Flow. | None |
| Actor or workflow is identified | Met | Debezium emits a Kafka message; worker parses and dispatches it. | None |
| Missing or incorrect behavior is identifiable | Met | Parser tests covered create, update, read, delete, tombstone, and invalid JSON, but not truncate. | None |
| Expected behavior is precise enough for acceptance test | Met | `op = t` should produce `ChangeOperation.Truncate` with no row payload. | None |
| Input, output, error state, and boundaries are clear | Met | Input is a Debezium envelope with `op = t`; output is a typed change event. | None |
| No conflicting documentation | Met | No documents conflict with truncate support. | None |
| Affected code area is identifiable | Met | `DebeziumEnvelopeParser`, `ChangeOperationMapper`, parser tests. | None |
| Suitable test infrastructure exists | Met | Existing xUnit project. | None |
| Enough time remains | Met | Scope is a single regression test plus trace documentation. | None |

## Decision

DECISION = CODE_NOW

The documentation is sufficient to harden one behavior: Debezium truncate events are listed in `docs/cdc-flow.md`, the worker flow routes parsed events to the customer handler, and current tests did not assert parser behavior for `op = t` or handler truncation. The planned action for this run is to add focused regression tests and record traceability here.

## Second Pass Decision

DECISION = CODE_NOW

The documentation is sufficient to harden configuration validation for retry and DLQ behavior. `docs/cdc-flow.md` lists `Kafka__RetryDelaySeconds`, `Kafka__MaxProcessingAttempts`, `Kafka__DeadLetterTopic`, and `Kafka__EnableDeadLetterTopic`; `docs/architecture.md` describes failed-message retry and DLQ publication as recoverability behavior. The action for this run is to add focused validation regression tests without changing runtime behavior.
