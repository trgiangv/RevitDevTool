---
name: revit-api-docs
description: Retrieve and analyze Revit API documentation through the local Revit API Docs MCP server. Use when answering questions about Autodesk Revit API classes, methods, properties, version differences, obsolete or changed APIs, migration between Revit 2020-2027, or when writing/reviewing Revit API code that needs authoritative docs.
---

# Revit API Docs

Use the `revit-api-docs` MCP server before answering version-sensitive Revit API questions or writing code that depends on Revit API behavior.

The MCP server is remote-first. It does not require a local SQL server, local docs
checkout, or manual sync. It creates a private SQLite cache file automatically and
uses it only to reduce repeated network and token cost.

## Workflow

1. Determine the target Revit version. For this repo, default to `2026` unless the project configuration or user request names another version.
2. Search narrowly with `revit_docs_search(query, version, kind?, namespace?, limit?)`.
3. Retrieve only needed sections with `revit_docs_get(symbol_or_href, version, sections, max_chars)`.
4. Read `api_card` first to understand purpose, use cases, lifecycle, constraints, and key related APIs.
5. Use `members` and `related` from `revit_docs_get` to navigate API tables; never manually scrape docs HTML in the agent.
6. For symbol migration, call `revit_docs_compare(from_version, to_version, symbol_or_href)`.
7. For release-level "what is new between versions" questions, call `revit_docs_compare(from_version, to_version)` without `symbol_or_href`.

## Token Rules

- Prefer `search` before `get`; do not fetch pages speculatively.
- Keep `limit <= 8` for search unless the user needs exhaustive discovery.
- Request specific sections: `syntax`, `parameters`, `remarks`, `exceptions`, `examples`, or `see_also`.
- Prefer `api_card` for the first answer pass; fetch member detail pages only when syntax or edge behavior is needed.
- Use structured `members` before fetching member detail pages. This is the low-token path for "related API" questions.
- Do not paste full docs. Summarize the relevant behavior and cite the returned URL.
- Never answer broad version-diff questions by fetching pages one by one; use `revit_docs_compare` without `symbol_or_href`.
- Treat release-level compare output as a compact index diff. Fetch individual pages only for representative APIs or user-selected symbols.

## MCP Run Command

Use this command when configuring a local MCP client from the repo root:

```bash
uvx --from .agents/mcp/revit-api-docs revit-api-docs-mcp
```

Required setup: install `uv`/`uvx`, then add the command server to the MCP client.

Optional setup:

- Set `REVIT_API_DOCS_CACHE` only if the default cache location is not acceptable.
- Set `REVIT_API_DOCS_SOURCE` only when the user explicitly provides a static mirror of the website.

Normal use should not set `REVIT_API_DOCS_SOURCE`; the server should use remote docs plus its auto-created SQLite cache.
