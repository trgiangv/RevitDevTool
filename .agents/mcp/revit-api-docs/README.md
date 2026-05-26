# Revit API Docs MCP

Remote-first MCP server for retrieving compact, version-aware Revit API Docs context.

No external database, local SQL server, or pre-sync step is required. The server fetches
`revapidocs.com` on demand and keeps a small local SQLite file as an implementation
cache only.

## Requirements

- `uv` or `uvx` available on `PATH`.
- Network access to `https://revapidocs.com`.
- An MCP client that supports stdio command servers.

## Run Command

From the `RevitDevTool` repo root:

```bash
uvx --from .agents/mcp/revit-api-docs revit-api-docs-mcp
```

The server uses stdio transport, so MCP clients should configure it as a command server.

## MCP Client Config

Use this shape in MCP clients that accept JSON config:

```json
{
  "mcpServers": {
    "revit-api-docs": {
      "command": "uvx",
      "args": [
        "--from",
        "C:/Users/truon/source/repos/RevitDevTool/.agents/mcp/revit-api-docs",
        "revit-api-docs-mcp"
      ]
    }
  }
}
```

Use an absolute path in client config. Relative paths may resolve from the client process directory, not the repo root.

## Storage Model

The default mode is remote docs plus an auto-created SQLite cache:

- No local SQL service is needed.
- No manual schema migration is needed.
- No `docs sync` command exists.
- Cache file defaults to `%LOCALAPPDATA%/RevitDevTool/revit-api-docs-mcp/revit_api_docs.sqlite3` on Windows.
- Set `REVIT_API_DOCS_CACHE` only if you want the cache file somewhere else.

## Optional Environment

Set a custom cache location:

```json
{
  "mcpServers": {
    "revit-api-docs": {
      "command": "uvx",
      "args": [
        "--from",
        "C:/Users/truon/source/repos/RevitDevTool/.agents/mcp/revit-api-docs",
        "revit-api-docs-mcp"
      ],
      "env": {
        "REVIT_API_DOCS_CACHE": "C:/Users/truon/AppData/Local/RevitDevTool/revit-api-docs-mcp"
      }
    }
  }
}
```

Set `REVIT_API_DOCS_SOURCE` only when you explicitly have a local static mirror of the website:

```json
"env": {
  "REVIT_API_DOCS_SOURCE": "D:/docs/revitapidocs-mirror"
}
```

Do not set `REVIT_API_DOCS_SOURCE` for normal use. It is only a performance/debug
override for a folder that contains `2020.htm` through `2027.htm` and matching
version subfolders.

## Available Tools

- `revit_docs_search`: Search symbols, members, namespaces, and descriptions.
- `revit_docs_get`: Retrieve one symbol page with selected sections plus compact `api_card`, `members`, and `related` links when available.
- `revit_docs_compare`: Compare either one symbol across versions or broad release-level API changes.

## Recommended Usage

1. Call `revit_docs_search` first with a narrow query and `limit <= 8`.
2. Call `revit_docs_get` only for the selected symbol.
3. Read `api_card` first for purpose, use cases, lifecycle, constraints, and key related APIs.
4. Use returned `members` to discover properties, methods, constructors, and related API pages. Do not scrape HTML manually.
5. Request specific sections such as `syntax`, `parameters`, `remarks`, `exceptions`, or `examples`.
6. For broad questions like "new APIs in Revit 2026 vs 2025", call `revit_docs_compare(from_version=2025, to_version=2026)` with no `symbol_or_href`.
7. For a specific symbol migration, call `revit_docs_compare(from_version, to_version, symbol_or_href)`.

The broad compare path reads only the yearly navigation indexes, not every detail
page. This is the intended low-token path for release-diff questions.

## Smoke Test

Run a quick direct import test:

```bash
cd C:/Users/truon/source/repos/RevitDevTool/.agents/mcp/revit-api-docs
uv run python -c "from revit_api_docs_mcp.server import revit_docs_search; print(revit_docs_search('Transaction', limit=1))"
```

Run a compact release-diff test:

```bash
uv run python -c "from revit_api_docs_mcp.server import revit_docs_compare; print(revit_docs_compare(2026, 2027)['counts'])"
```

Start the MCP server:

```bash
uvx --from C:/Users/truon/source/repos/RevitDevTool/.agents/mcp/revit-api-docs revit-api-docs-mcp
```

The second command waits on stdio for an MCP client. No output means the server started and is waiting for protocol messages.
