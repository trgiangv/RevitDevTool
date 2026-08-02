# Python MCP SDK 2.0 Runtime Upgrade

Date: 2026-08-01

## Status

Validated — live smoke on Revit 2025 (PID 44780) with Python toolset only.

## Outcome

Host Python runtime and sample Python MCP toolset run on conda-forge / PyPI
`mcp==2.0.0` (`FastMCP` → `MCPServer`), with:

- `source/DevTools.Execution/Resources/scripts/ToolParser.py`
- `source/DevTools.Execution/Resources/scripts/ToolInvoke.py`
- `samples/PythonDemo/mcp_toolset/**`
- pins in `PyEnvironmentProvider.RequirePackages`, runtime `pixi.toml`, and
  root `pixi.toml` (dev workspace)
- host env `%APPDATA%/RevitDevTool/pixi-env` upgraded (`pixi install` → mcp 2.0.0)

Live proof (2026-08-01):

1. Registry `%APPDATA%/RevitDevTool/2025/Settings/McpRegistryConfig.json`:
   `dotnetToolsetPaths: []`, `pythonToolsetPaths: [samples/PythonDemo/mcp_toolset]`
2. Deploy: `scripts/kill-host.ps1` + `scripts/build-host.ps1 -Year 2025`
3. `launch_host(Revit, 2025)` → PID 44780, bridge connected
4. `search_dynamic(query="revit_", hostInstanceId=44780)` → Python tools with
   snake_case `argsHint` (`view_id`, `element_ids`, …); `hasMore: true`
5. `invoke_dynamic(capabilityId=revit_get_status)` → `ok`, structured
   `{healthy:false}` (no document open)
6. `invoke_dynamic(revit_list_views / revit_find_elements)` → Python path
   reached; domain error `No active Revit document` (expected, no .rvt open)
7. Built-ins still present (`execute_*`, `open_document`); C# `RevitMcpToolSet`
   not registered

## Context

- Product: `docs/product/mcp.md` (external contract unchanged)
- Prior deferral: `docs/plans/active/2026-07-27-mcp-dynamic-discovery-and-python-toolset.md`
  marked `mcp==2.0.0rc1` out of scope; stable `2.0.0` is now on PyPI
- Migration guide: sibling `python-sdk/docs/migration.md` (`FastMCP` → `MCPServer`)

## Scope

In scope (done):

1. Raise pins to `mcp>=2.0,<3`
2. Port all `from mcp.server.fastmcp import FastMCP` call sites to `MCPServer`
3. Re-bootstrap / upgrade pixi env so Revit host picks up 2.0
4. Focused live verify with Python toolset only

Out of scope:

- Changing daemon external `capabilityId` contract
- Dual-registering Python + C# toolsets with identical `revit_*` names
- Upstream python-sdk alias dump bug (still use snake_case params)
- Opening a sample .rvt for richer query results (no local sample found)

## Validation

- Host `importlib.metadata.version("mcp")` → `2.0.0`; `from mcp.server.mcpserver import MCPServer` OK
- Parser loads `samples/PythonDemo/mcp_toolset/revitdevtool_mcp.py` (catalog populated)
- Live: `search_dynamic` + `invoke_dynamic` against Python toolset tools
