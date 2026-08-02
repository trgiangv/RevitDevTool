# Known Test Gaps

Relative coverage of **this repo's** `tests/` — grouped by product area, not by individual
test file. Passing unit tests does not prove live host behavior, daemon reload, or full
year-matrix packaging.

Client pytest (`RevitDevTool.PyTest`) is a separate repo; only host-side bridge code
here is in scope.

## Summary

| Area | Relative coverage | Notes |
|------|-------------------|-------|
| MCP | **Medium–high** | Strong on contracts, parsers, harnesses; weak on live host E2E |
| Pytest bridge | **Low** | Framing + pipe naming only |
| Execution | **Low** | Pip/guard smoke; script engines largely untested |

---

## MCP

Largest test surface (`tests/DevTools.Mcp.Tests`, ~180 cases).

### Well covered

- **Protocol & models** — JSON-RPC framing, host handler routing, conformance subset
- **Daemon composition** — server builder, fixed tools, `search_dynamic` / `invoke_dynamic` harness
- **Catalog & encoding** — host catalog merge, list/response encoders, dynamic tool contracts
- **Toolset discovery** — .NET + Python parsers, argument binding, result/MRTR mapping, ALC bridges
- **SDK integration** — stream transport contracts, in-process named-pipe round-trip (mock host)

### Partial / fragile

- **Connection tracking** — endpoint/client count goes through main-thread UI helper; headless xUnit is unreliable
- **Parser integration** — needs built sample toolsets + pixi env; sample metadata can drift during SDK migration
- **Named pipe to real host** — harness tests exist; full daemon → deployed host → tool invoke chain is not automated

### Gaps (low automated coverage)

- **Live host** — `execute_*` tools, tasks opt-in, threading — manual checklist (`mcp-integration-test.md`) or running Revit
- **Built-in execution tools** — C#/Python code paths on host thread
- **Packaging & reload** — ILRepack year matrix, daemon hot-reload, shared-runtime layout (some packaging asserts, not full matrix)
- **End-to-end toolset invoke** — catalog discovery → host dispatch → Revit API (no single CI test)

**Prerequisites:** build `samples/McpToolsetDemo` and/or `samples/RevitMcpToolSet` for parser/spike tests; pixi env at `%APPDATA%\RevitDevTool\pixi-env` for Python parser tests (`scripts/test-python.ps1`).

---

## Pytest bridge

Host pipe (`DevTools_*`) is separate from MCP pipe (`DevToolsMcp_*`).

### Well covered

- **Wire identity** — pipe name format and pytest vs MCP discrimination
- **Framing** — length-prefixed `BridgeMessage` distinct from MCP NDJSON

### Gaps (low automated coverage)

- **`DevToolsPipeServer`** — accept loop, lease, error paths
- **Request routing** — `PytestRequestHandler`, `instance/*` vs `tests/run`
- **In-host runner** — `PytestExecutionService`, `PytestRunner.py`, dependency install
- **Progress & batch modes** — streaming notifications vs IDE batch responses
- **Cross-process flow** — discover → connect → run → report (covered in client repo, not host CI)

---

## Execution

Thin unit layer (`tests/DevTools.Execution.Tests`, ~23 cases).

### Well covered

- **Python environment** — pip/pixi path resolution, env probing
- **Execution guard** — `ExecutionGuardContext` ambient mode / rollback summary

### Gaps (low automated coverage)

- **Script engines** — C#, F#, IronPython, assembly strategies; compilation cache and load contexts
- **Orchestration** — `ExecutionOrchestrator`, package install/version check, file watcher, tree state
- **Host threading** — `IHostContextExecutor`, main-thread marshaling for API calls
- **MCP dispatch from execution** — `McpPrimitiveDispatcher`, `ToolInvoke.py` payload path
- **Built-in tools** — open document, registry providers

`tests/RevitDevTool.PyServer.Tests/` — small Python parser check only.

---

## Verification

| Goal | Command |
|------|---------|
| MCP tests | `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` |
| All .NET tests | `scripts/test-dotnet.ps1` |
| Python parser | `scripts/test-python.ps1` |
| Live MCP | `docs/agents/mcp-integration-test.md` |

## Reporting

State the **feature gap** (e.g. "pytest bridge: no `tests/run` handler test"), missing
build artifact, or environment (pixi, host PID). Do not claim full platform verification
when only compile or a single project passed.
