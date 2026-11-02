# Architecture decision records

These records document implemented decisions, reconstructed from source on
2026-11-02; the date is the documentation date, not the original adoption date.
Accepted means implemented for the local demo, not production certification.

| Record | Decision | Status |
| --- | --- | --- |
| [0001](0001-cdc-delivery.md) | Manual Kafka commits, bounded retries and dead letters | Accepted |
| [0002](0002-scoped-topic-dispatch.md) | Typed topic subscriptions with a scope per attempt | Accepted |
| [0003](0003-local-extractive-qa.md) | Independent SQLite and BM25 extractive document QA | Accepted |
| [0004](0004-tool-contracts.md) | Provider-independent validated tool registry | Accepted |
| [0005](0005-tool-action-security.md) | Default-deny permissions and single-use confirmations | Accepted |

[Sequence diagrams](../sequence-diagrams.md) describe runtime behavior and failure
paths. Each record includes context, alternatives, consequences and evidence.
For a new decision, add the next numbered file with those sections, date and
status. Preserve old records; mark them superseded and link the replacement when
the decision changes. Proposed designs must be labeled Proposed, not Accepted.
