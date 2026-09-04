# MCP And PyTest Bridge Digest

Deep sources: `docs/architecture/MCP/README.md`, `docs/architecture/Execution/pytest-bridge.md`.

## Two pipes

| Pipe | Server | Use |
|------|--------|-----|
| `DevToolsMcp_{Host}_{Version}_{PID}` | `HostMcpPipeServer` | MCP SDK (NDJSON) |
| `DevTools_{Host}_{Version}_{PID}` | `DevToolsPipeServer` | pytest + control (`BridgeMessage`) |

Never mix NDJSON and `BridgeMessage` on one pipe.

## MCP

Daemon (`--stdio` or Gateway) exposes infrastructure tools plus `search_dynamic` / `invoke_dynamic`. Host tools stay in `ConnectedHostCatalog`. In-host built-ins: `execute_csharp_code`, `execute_python_code`, `open_document`, `navigate_history`. See [MCP/tools.md](../architecture/MCP/tools.md).

Paths: `DevTools.Ipc`, `DevTools.Mcp.*`, `DevTools.Daemon`, `DevTools.Execution/External/`.

## PyTest (host side, this repo)

- `tests/run` → `PytestRunner.py` (PEP 723 via the active provider — uv sidecar when the host owns CPython).
- `ipytests/run` → `IpyTestDriver.py` (unittest, no pixi).
- Contracts: `PytestContracts.cs` ↔ sibling `RevitDevTool.PyTest` `models.py`.
- Write/run tests: `.agents/skills/revit-pytest/SKILL.md`.

## Change checklist

- MCP: `dotnet run --project tests/DevTools.Mcp.Server.Tests/DevTools.Mcp.Server.Tests.csproj`; parsers: `tests/DevTools.Mcp.Catalog.Tests` (build `samples/McpToolsetDemo` first, or tests Skip).
- Pytest wire: sync contracts ↔ `models.py`; focused Execution tests (`PytestRunRequestParseTests`, `DevToolsPipeServerTests`, handlers).
- Remaining live/opt-in and Coverlet/testhost limits: `test-matrix.md` **Current gaps**. In-host `PytestRunner.py` is not headless CI.
