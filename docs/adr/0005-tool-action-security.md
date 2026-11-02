# ADR 0005: Default-deny authorization and single-use action confirmations

- Status: Accepted for a single local server process
- Documented: 2026-11-02

## Context

Schema-valid actions can still read another user's documents or delete data without
consent. All HTTP entry points must apply the same policy, including legacy routes.

## Decision

Map opaque bearer credentials to server-configured ExecutionContext identities.
Require read/write/delete scopes and an explicit document allowlist (null grants
all; empty grants none). Filter list/search/answer results by that access boundary.
Require prepare followed by explicit client confirmation and invoke for every
mutation, including creates. Bind random confirmation tokens to subject, tool and
argument hash; expire after 120 seconds and consume once. Recheck permissions at
invoke. Serialize policy checks, quota checks and execution under policy/KB locks.

Limit tool calls per subject, total document count/content characters and pending
confirmations. Persist redacted prepare/invoke outcomes in SQLite. Set a socket
read timeout and body limit. Keep credentials in UI memory; show confirmation before
saving. Startup requires DOCUMENT_QA_IDENTITIES.

## Alternatives

- Trust client-supplied roles or an LLM's approval: caller could grant itself authority.
- A confirmed=true flag: not bound to arguments and trivially reusable.
- Shared durable approval/rate services: appropriate for multiple workers, more deployment complexity.
- Confirmation only for deletes: less friction, but replacement also destroys old content.

## Consequences and boundaries

Missing credentials fail closed. Scope and resource checks are separate. Tokens
are not idempotency keys: replay is rejected rather than returning a cached result.
Preparing a token does not prove human approval; trusted UI must control the
approval step and must not let a model approve its own action. Tokens are not bound
to a document revision, so this is not optimistic concurrency control.

Rate and confirmation state live in RAM; restarts clear both. Multiple processes
need shared state and distributed coordination. Audit persistence is separate from
mutation transactions: output validation or audit failure after a committed write
can yield an error despite changed data. There is no automatic rollback or hard
execution timeout. Pre-dispatch HTTP authentication failures are not in tool_audit.
Audit retention/rotation and external identity lifecycle management remain operational
work. Local CLI ingestion and direct DB access are trusted administrative paths
outside the tool policy. These are not tenant-isolated databases.

## Evidence and revisit criteria

See [policy](../../document_qa/security.py), [HTTP boundary](../../document_qa/server.py),
[security tests](../../document_qa/tests/test_security.py) and [configuration guide](../document-qa.md#authorization-confirmation-và-guardrails).
Revisit before multi-worker hosting, remote exposure, durable approvals or
non-repeatable external effects. See [mutation sequence](../sequence-diagrams.md#confirmed-tool-mutation).
