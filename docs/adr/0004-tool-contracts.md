# ADR 0004: Provider-independent validated tool registry

- Status: Accepted
- Documented: 2026-11-02

## Context

Direct JSON-to-method HTTP dispatch does not expose a reusable tool contract and
allows input/output drift between callers and implementations.

## Decision

Publish five fixed tools through ToolRegistry with version, mutation metadata,
required scope, confirmation metadata and input/output JSON Schemas. Validate
arguments before execution and results before returning them. Reuse the dispatcher
from both the generic invoke endpoint and legacy document endpoints. Reject unknown
tools and object fields; sanitize unexpected execution exceptions. Keep registry
execution independent of an LLM provider.

## Alternatives

- Provider-specific tool definitions: quicker integration with one model but duplicated contracts.
- Independent validation per HTTP route: simple initially but inconsistent over time.
- A full JSON Schema dependency: broader support but unnecessary for the current keyword subset.

## Consequences

Callers can discover contracts at GET /api/tools. The standard-library validator
implements only the keywords used by these schemas; the advertised schema dialect
does not mean arbitrary schemas can be validated. New keywords need implementation
and tests. Output validation happens after execution and cannot undo a write.
Metadata describes policy; enforcement comes from the security layer. There is no
MCP protocol server, agent loop or automatic model tool selection.

## Evidence and revisit criteria

See [registry](../../document_qa/tools.py), [server](../../document_qa/server.py)
and [contract tests](../../document_qa/tests/test_tools.py).
Revisit for broader schema features, provider adapters or incompatible contract changes;
version those changes explicitly. See [security ADR](0005-tool-action-security.md).
