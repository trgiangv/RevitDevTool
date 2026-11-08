# MCP And PyTest Bridge Digest

Deep sources: `docs/architecture/MCP/README.md`, `docs/architecture/Execution/pytest-bridge.md`.

## Two pipes

| Pipe | Server | Use |
|------|--------|-----|
| `DevToolsMcp_{Host}_{Version}_{PID}` | `McpPipeServer` | MCP SDK (NDJSON) |
| `DevTools_{Host}_{Version}_{PID}` | `DevToolsPipeServer` | pytest + control (`BridgeMessage`) |

Each pipe carries one framing: NDJSON or `BridgeMessage`, never both.

## MCP

Daemon (`--stdio` or Gateway) exposes four tools: `list_processes`, `launch_host`, `read_file_info`, and `code_mode`. Host tools and resources are indexed in `ProcessCatalogs` (not on daemon `tools/list`) and are reached from a `code_mode` C# program through `SearchAsync`, `DescribeAsync`, `InvokeAsync`, and `ReadAsync`. In-host built-ins: `execute_csharp_code`, `execute_python_code`, `open_document`, `navigate_history`, `view_screenshot`, `run_command`, `add_tools`. See [MCP/tools.md](../architecture/MCP/tools.md) and [mcp-integration-test.md](mcp-integration-test.md#calling-the-host-from-code_mode).

Paths: `DevTools.Ipc`, `DevTools.Mcp.*`, `DevTools.Daemon`, `DevTools.Execution/External/`.

## PyTest (host side, this repo)

- `tests/run` → `PytestRunner.py` (PEP 723 via the active provider — uv sidecar when the host owns CPython).
- `ipytests/run` → `IpyTestDriver.py` (unittest, no pixi).
- Client: `source/RevitDevTool.PyTest` (`pytest` product). Contracts: `PytestContracts.cs` ↔ `models.py`, held together by the golden fixtures in `source/RevitDevTool.PyTest/tests/golden/` (`GoldenWireTests` in C#, `test_golden.py` in Python); a wire change edits the fixtures and both sides in one PR. Known difference: a missing `engine` reads as `""` in C# and `None` in Python; the fixtures set it.
- Versions ([0055](../decisions/0055-product-version-wire-contracts-and-satellite-compatibility.md)): `instance/info` reports `productVersion` and `protocols.pytest { current, min }` (`PytestProtocol`); the client checks it after connecting. Each `tests/run` / `ipytests/run` carries `sender { version, protocol }`; the host rejects a missing or non-overlapping one with one sentence. Bump `PytestProtocol` and `constants.py` `PROTOCOL_*` in the same PR.
- Write/run tests: `.agents/skills/revit-pytest/SKILL.md`.

## Change checklist

- MCP: `dotnet run --project tests/DevTools.Mcp.Server.Tests/DevTools.Mcp.Server.Tests.csproj`; parsers: `tests/DevTools.Mcp.Catalog.Tests` (build `samples/McpToolsetDemo` first, or tests Skip).
- Pytest wire: sync contracts ↔ `models.py` and the golden fixtures; `pixi run test` in `source/RevitDevTool.PyTest`; focused Execution tests (`GoldenWireTests`, `PytestRunRequestParseTests`, `PytestProtocolTests`, `DevToolsPipeServerTests`, handlers).
- Live, opt-in, and testhost limits: [test-matrix.md](test-matrix.md#current-gaps). In-host `PytestRunner.py` is not in headless CI.
