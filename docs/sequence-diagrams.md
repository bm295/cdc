# Runtime sequence diagrams

These diagrams describe implemented behavior documented on 2026-11-02.
[ADRs](adr/README.md) explain decisions and alternatives. Arrows are logical calls,
not distributed transaction guarantees. Each diagram keeps at most five participants.

## CDC dispatch and projection

```mermaid
sequenceDiagram
    participant K as Kafka
    participant L as Consumer loop
    participant D as Dispatcher and subscription
    participant H as Customer handler
    participant R as Replica store
    K->>L: Consume record
    L->>D: Dispatch raw ConsumedMessage
    D->>D: Route exact topic and create scope
    D->>D: Parse typed Debezium envelope
    alt Tombstone
        D-->>L: Complete without handler
    else Row change or truncate
        D->>H: Resolve scoped handler and handle change
        H->>R: Upsert, delete or truncate replica
        R-->>H: Write completed
        H-->>D: Completed
        D-->>L: Completed
    end
    L->>K: StoreOffset then Commit
```

Each retry repeats dispatch with a new scope. Parsing belongs to the subscription,
not the consumer loop. Only the success branch is shown here; exceptions follow
the next diagram. Writes and offset commits are separate, so replay is possible.

## CDC retries and dead letters

```mermaid
sequenceDiagram
    participant K as Kafka
    participant L as Consumer loop
    participant D as Dispatcher
    participant Q as Dead-letter producer
    K->>L: Consume record
    loop Up to MaxProcessingAttempts
        L->>D: Dispatch same record
        D-->>L: Failure
        L->>L: Delay only before another attempt
    end
    alt DLQ enabled and publish succeeds
        L->>Q: Publish payload and failure metadata
        Q-->>L: Publication completed
        L->>K: StoreOffset then Commit
    else DLQ disabled or publication fails
        L->>L: Propagate error to outer loop
        L->>L: Delay and recreate consumer
        Note over K,L: No offset commit on this failure path
    end
```

This depicts a record that fails on every attempt. Earlier success skips DLQ and
commits normally. Commit failure after a write or DLQ publication can replay work.
Cancellation propagates without intentionally dead-lettering the cancelled action.

## Authorized document question

```mermaid
sequenceDiagram
    participant C as Client
    participant A as HTTP API
    participant T as Registry and policy
    participant B as KnowledgeBase
    participant S as SQLite
    C->>A: POST ask with bearer credential
    A->>A: Resolve trusted execution context
    alt Missing or invalid credential
        A-->>C: 401
    else Authenticated
        A->>T: Invoke answer_question with context
        T->>T: Validate schema, read scope and rate
        alt Policy denied
            T->>S: Audit denial
            T-->>A: ToolError
            A-->>C: 403 or 429
        else Allowed
            T->>B: Ask with allowed document ids
            B->>S: Load passages
            S-->>B: Rows
            B->>B: Filter allowed ids before BM25 scoring
            B-->>T: Extractive answer or abstention
            T->>T: Validate output schema
            T->>S: Audit success
            T-->>A: Result
            A-->>C: 200 with authorized citations
        end
    end
```

Invalid input is 400. Unexpected execution/output errors are sanitized as 500.
The in-process lock covers policy and KB execution. HTTP authentication failures
occur before dispatcher audit. SQLite reads still load the corpus before filtering.

## Confirmed tool mutation

```mermaid
sequenceDiagram
    participant U as User
    participant C as Trusted client UI
    participant A as HTTP API
    participant T as Registry and policy
    participant B as KnowledgeBase and SQLite
    U->>C: Request create, replace or delete
    C->>A: POST tools/prepare with bearer and arguments
    A->>T: Prepare using authenticated context
    T->>T: Validate input, scope, resource and rate
    T->>T: Store token bound to subject, tool, argument hash and expiry
    T->>B: Audit prepared
    T-->>C: Token and action summary via API
    C->>U: Display action and ask for confirmation
    alt User cancels
        C->>C: Stop without invoking
    else User approves
        U->>C: Confirm
        C->>A: Invoke exact arguments with confirmation header
        A->>T: Invoke using authenticated context
        T->>T: Lock, validate, recheck permission and rate
        T->>B: Check quota for upsert
        T->>T: Check token binding and expiry, consume once
        T->>B: Execute mutation
        B-->>T: Mutation committed
        T->>T: Validate output
        T->>B: Audit success
        T-->>C: Result via API
    end
```

Current UI implements this interaction for saving documents; API clients implement
it for deletes as well. Quota checking is only for upsert. Permission checks are
repeated for prepare and invoke. Audit is not atomic with mutation. A token proves
the protocol step, not independent human presence. Cancelled tokens expire in RAM.

## Mutation rejection and uncertain outcomes

```mermaid
sequenceDiagram
    participant C as Client
    participant T as Registry and policy
    participant B as KnowledgeBase and SQLite
    C->>T: Invoke mutation
    alt Permission or rate denied
        T->>B: Audit denial
        T-->>C: forbidden or rate_limited
    else Upsert quota exceeded
        T->>B: Audit denial
        T-->>C: quota_exceeded
    else Token missing, expired, mismatched or consumed
        T->>B: Audit denial
        T-->>C: confirmation_required or invalid_confirmation
    else Checks pass
        T->>T: Consume token
        T->>B: Commit mutation
        alt Output validation fails
            T->>B: Audit execution failure
            T-->>C: tool_execution_failed
            Note over C,B: Mutation may already be committed
        else Output valid
            T->>B: Audit success
            T-->>C: Result
        end
    end
```

The diagram assumes audit writes succeed. Audit failure can itself prevent a response;
there is no atomic action/result ledger. A lost HTTP response also leaves the caller
uncertain. Do not blindly retry with a new confirmation: inspect document state first.
A consumed token is never restored automatically. See [ADR 0005](adr/0005-tool-action-security.md).
