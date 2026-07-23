# PyTest Bridge Product Contract

Local pytest collects tests; the host process executes them over a Named Pipe
JSON-RPC bridge. Supports Revit, AutoCAD-family, and any host exposing a
`DevToolsPipeServer` pipe.

## Behavior

- Client plugin lives in sibling repo `RevitDevTool.PyTest`; always run with
  `uv run pytest` from that repo root.
- Host options use `--host` / `--host-version` / `--host-pipe` / `--host-launch`
  (not legacy `--revit-*` flags).
- Wire models must stay mirrored between Python `models.py` and C#
  `PytestContracts.cs`.

## Related

- Architecture: [`docs/architecture/PyTest/README.md`](../architecture/PyTest/README.md)
- Client AGENTS: sibling `RevitDevTool.PyTest/AGENTS.md`
- Agent digest: [`docs/agents/mcp-pytest-bridge.md`](../agents/mcp-pytest-bridge.md)
