# 0044 Hybrid Retrieval Evaluation for MCP Tool Discovery

Date: 2026-10-20; updated 2026-10-22

## Status

**Proposed evaluation only.** This ADR does not authorize embeddings, a model
runtime, or a vector index in the production search path. Decision
[0040](0040-bm25f-search-and-hit-annotations.md) remains the accepted retrieval
contract and implementation.

## Purpose

Decide whether semantic retrieval measurably improves capability selection
beyond the current schema-aware lexical search. The question is not whether
embeddings can retrieve paraphrases in general; it is whether they improve
RevitDevTool's real catalog without hiding exact tool/schema matches, weakening
safety choices, or exceeding an agreed latency and resource budget.

## Current baseline

Decision 0040 is the source of truth for search behavior. In brief, the Daemon
indexes catalog identity, descriptions, and nested MCP input/output schemas;
uses BM25F with a query-coverage gate; and suppresses generic execution tools
when a specialized capability passes relevance. If none does, execution tools
are the fallback; explicit execution-intent queries may surface them directly.
A constrained attribute-query rule uses generic parameter/value schemas for
requests such as “doors with fire rating.” There is no general synonym table,
stemming, or semantic model.

The search result is a ranked candidate list, not an instruction to invoke the
first item. `code_mode` should inspect the candidate descriptions and, when
needed, the tool schema before choosing a capability.

### Live smoke-test evidence

After publishing the current Daemon, these read-only `SearchAsync` queries were
run against a connected Revit 2025 catalog:

| Query | Results, in order |
|---|---|
| `find walls by category` | `revit_find_elements`, `revit_export_to_excel` |
| `filter elements by category` | `revit_find_elements`, `revit_export_to_excel` |
| `show doors with fire rating` | `revit_export_to_excel`, `revit_find_elements` |
| `delete elements preview` | `revit_delete_elements` |
| `schedule` | preview, export, create, list fields, place on sheet |
| `preview schedule` / `export schedule` | matching preview / export tool first |

This is a smoke test, not a relevance benchmark: it is one live catalog and a
small, hand-picked query set. It shows that common searches find specialized
tools and that explicit actions distinguish schedule operations. It also shows
a remaining ranking ambiguity: a broad “show” request can rank export ahead of
search when both schemas match the requested data.

The benchmark in
[`SearchIndexBenchmarkTests.cs`](../../tests/DevTools.Mcp.Client.Tests/SearchIndexBenchmarkTests.cs)
measures index and query performance on a synthetic catalog. It has no
human-judged relevance labels and cannot establish semantic quality.

## Proposal

Keep BM25F as the deterministic lexical channel. If a relevance evaluation
shows a material gap, compare a semantic candidate channel over concise,
structured capability profiles. Merge candidate rankings with a rank-fusion
method; do not add raw BM25F and cosine scores without calibration.

A profile may summarize fields already present in the catalog:

- purpose and expected outcome;
- applicable objects or data domains;
- parameter names, types, descriptions, and enum values;
- preconditions, side effects, and tool annotations when available;
- a few validated natural-language examples, if reliable source metadata
  supports them.

Keep the MCP schema and tool declaration authoritative. A generated profile is
search metadata only; it must not create invocation arguments or imply an
authorization guarantee.

For the current catalog scale, compare an exact in-memory vector scan before
considering approximate-nearest-neighbor infrastructure. Treat model loading,
query encoding, memory, and cold-start cost as part of the retrieval budget.
The default must not send user queries or catalog text to a remote service.

A bounded reranker is a separate experiment. Evaluate it only over the merged
top candidates and only if it yields repeatable quality gains within the
latency budget. Multi-step queries must retain multiple required capabilities
rather than being collapsed to a single best match.

## Evaluation gate

Before changing the production default:

1. Build a versioned, human-reviewed set of Revit and AutoCAD tasks. Include
   English and Vietnamese paraphrases, exact schema terms, vague requests,
   ambiguous near-duplicates, read-only/destructive constraints, and
   multi-capability tasks. Label acceptable capabilities and hard negatives.
2. Compare the current BM25F baseline, profile-enriched lexical search, dense
   retrieval, hybrid rank fusion, and hybrid plus bounded reranking on the same
   catalog and held-out queries.
3. Report `Recall@8`, `Recall@50`, `MRR@8`, `nDCG@8`, hard-negative confusion,
   multi-capability coverage, unsafe-selection rate, warm/cold p50 and p95,
   index build time, and memory. Report per-language and per-task slices.
4. Set quality and latency thresholds before looking at held-out results. Keep
   the current baseline unless the gain is repeatable, material, and within
   those limits.

Search ranking never overrides caller constraints. Process, primitive-kind,
and read-only restrictions remain hard filters or invocation-time checks.
Retrieval scores do not authorize a tool call.

## Alternatives

- **Improve the current lexical index/profile only:** preferred first response
  when failures are caused by missing or poor catalog metadata.
- **Replace BM25F with embeddings:** rejected as the sole channel; broad
  semantic matches can hide exact names and parameter identifiers.
- **Run an LLM/cross-encoder over the whole catalog:** rejected for the default
  path because cost and latency scale with every catalog item. A bounded
  reranker remains testable.
- **Add a hand-maintained synonym table:** rejected as the primary strategy;
  it creates ongoing vocabulary maintenance and does not model constraints or
  side effects.
- **Add ANN or a vector database now:** deferred until measured catalog size
  and latency justify the index lifecycle and tuning cost.

## Relationship to other decisions

- [0040](0040-bm25f-search-and-hit-annotations.md) remains authoritative for
  current search, fallback, and `code_mode` behavior.
- [0039](0039-mcp-flow-audit-sdk-reuse-and-vocabulary.md) is historical flow
  context; its supersession by 0040 is unchanged.
- This proposal does not change the `code_mode` envelope, host-tool schemas,
  annotations, invocation checks, or the rule that host capabilities stay off
  `tools/list`.

## References

- Khattab and Zaharia, [ColBERT: Efficient and Effective Passage Search via
  Contextualized Late Interaction over BERT](https://arxiv.org/abs/2004.12832),
  2020.
- Zheng et al., [ToolRerank: Adaptive and Hierarchy-Aware Reranking for Tool
  Retrieval](https://aclanthology.org/2024.lrec-main.1413/), LREC-COLING 2024.
- [Tool-REX: Tools are under-documented: Simple Document Expansion Boosts Tool
  Retrieval](https://proceedings.iclr.cc/paper_files/paper/2026/hash/e54a9889f1048526365ed8fb56c8c8cd-Abstract-Conference.html),
  ICLR 2026. Its results motivate evaluation; they do not predict gains on this
  repository's catalog.

## Follow-up

No embedding or reranking implementation is planned until the evaluation gate
is met and this proposal is accepted. If the baseline remains sufficient,
keep BM25F and improve authoritative catalog metadata instead.
