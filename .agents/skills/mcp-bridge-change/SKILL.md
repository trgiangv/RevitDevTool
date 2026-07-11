---
name: mcp-bridge-change
description: Review checklist for MCP bridge changes (protocol, catalog, dispatch, pipe server, daemon tools). Use when editing source/DevTools.Mcp/, source/DevTools.Ipc/, or source/DevTools.Daemon/Mcp/.
---

# MCP Bridge Change

Use when editing MCP parser, registry, standalone server, pipe protocol, or tool/prompt/resource dispatch.

## Checklist

- Read `docs/agents/mcp-pytest-bridge.md` and `docs/MCP/README.md`.
- Identify whether the change is parser-only, standalone server, in-host registry, or dispatcher.
- Verify both .NET and Python toolset implications when catalog shape changes.
- Keep configured path persistence and invalid-path pruning behavior intact.
- Preserve tool, prompt, and resource identity rules.
- Current tests are mostly parser/contract oriented. Add a narrow test when changing catalog identity, serialized protocol shape, path pruning, or dispatcher lookup.
- Update `docs/MCP/README.md` or `docs/agents/mcp-pytest-bridge.md` when changing MCP architecture, registry flow, protocol shape, or dispatch behavior.
- Run relevant .NET tests; if host/named-pipe verification is required but unavailable, report that blocker.
