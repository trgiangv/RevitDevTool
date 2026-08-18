# Product Docs

Current product behavior derived from accepted intent. Keep these documents
short and living. Deep design belongs in `docs/architecture/`; agent operating
notes belong in `docs/agents/`.

## Domains

| Document | Covers |
|----------|--------|
| [overview.md](overview.md) | Platform identity, hosts, solution truth |
| [assembly-isolation.md](assembly-isolation.md) | Runtime/metadata loading, identity, containment, and lifecycle contract |
| [execution.md](execution.md) | Code/script execution behavior |
| [mcp.md](mcp.md) | MCP entry points and host bridging |
| [pytest-bridge.md](pytest-bridge.md) | Remote pytest via Named Pipe |
| [nunit-host-testing.md](nunit-host-testing.md) | MTP-only in-host NUnit testing |
| [logging.md](logging.md) | Logging sinks and redirection |
| [visualization.md](visualization.md) | Revit-only DirectContext3D overlays |

## Update Rule

When behavior changes:

1. Update the affected product document.
2. Update `docs/architecture/<Module>/` when design/structure changed.
3. Update `docs/agents/` when agent workflow or verification traps changed.
4. Add a lasting decision under `docs/decisions/` only when future work must inherit it.
5. Add or update executable proof that exercises the behavior.

Bounded changes with no contract change do not require product edits.
