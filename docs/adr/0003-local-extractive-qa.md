# ADR 0003: Independent local extractive document QA

- Status: Accepted
- Documented: 2026-11-02

## Context

The repository needs runnable document questions, citations and evaluation without
requiring a model credential or the CDC infrastructure to be running.

## Decision

Keep document_qa independent of the .NET consumer. Store documents and overlapping
chunks in SQLite; replace document content and chunks transactionally on ingest.
Rank authorized passages using BM25 and return their text verbatim as numbered
citations. Return abstained=true when no passage matches. Use an offline evaluation
dataset and quality gate in CI. Retrieval is computed on demand over the corpus;
there is no vector index or remote inference.

## Alternatives

- A hosted generative model: more flexible answers, but adds cost, credentials and grounding risk.
- Embeddings and a vector database: better semantic matching potential, but adds infrastructure.
- Reuse Kafka for QA requests: unnecessary coupling for synchronous local retrieval.

## Consequences

Runs using Python standard library and can be tested offline. Keyword overlap is
not semantic relevance or confidence. Extractive integrity verifies assembly of
citations, not whether the selected passages answer the question. No automatic
translation, PDF extraction or multi-turn reasoning is provided. Full-corpus
loading/scoring is intended for small datasets. Read authorization filters passages
before scoring, but the implementation still loads database rows into process memory.

## Evidence and revisit criteria

See [core](../../document_qa/core.py), [evaluation](../../document_qa/evaluate.py),
[tests](../../document_qa/tests/test_qa.py) and [QA guide](../document-qa.md).
Revisit when corpus size, semantic recall or answer synthesis requires another
retriever/model. Measure on held-out cases first. See [QA sequence](../sequence-diagrams.md#authorized-document-question).
