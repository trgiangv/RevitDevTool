# Example: MCP Toolsets

Hands-on walkthrough for registering sample toolsets and invoking them from an AI client. For authoring guides, see [MCP .NET](/docs/mcp/MCP-CSharp) and [MCP Python](/docs/mcp/MCP-Python).

## Samples

| Sample | Link | Backend |
| --- | --- | --- |
| Python toolset | [samples/PythonDemo/mcp_toolset](https://github.com/trgiangv/RevitDevTool/tree/main/samples/PythonDemo/mcp_toolset) | Python folder + `*mcp.py` entry file |
| .NET demo | [samples/McpToolsetDemo](https://github.com/trgiangv/RevitDevTool/tree/main/samples/McpToolsetDemo) | C# assembly (minimal) |
| Revit toolset | [samples/RevitMcpToolSet](https://github.com/trgiangv/RevitDevTool/tree/main/samples/RevitMcpToolSet) | C# assembly (full Revit surface) |

`RevitMcpToolSet` and `PythonDemo/mcp_toolset` implement the same tool surface per [TOOLSET-SPEC.md](https://github.com/trgiangv/RevitDevTool/blob/main/samples/TOOLSET-SPEC.md). Load **one** implementation per host instance — do not register Python and C# toolsets with overlapping tool names at the same time.

## What Each Sample Shows

| Sample | Purpose |
| --- | --- |
| [McpToolsetDemo](https://github.com/trgiangv/RevitDevTool/tree/main/samples/McpToolsetDemo) | .NET tools, prompts, and resources — parser and schema edge cases |
| [RevitMcpToolSet](https://github.com/trgiangv/RevitDevTool/tree/main/samples/RevitMcpToolSet) | Production-style Revit MCP tools, resource templates, prompts |
| [PythonDemo/mcp_toolset](https://github.com/trgiangv/RevitDevTool/tree/main/samples/PythonDemo/mcp_toolset) | Python layout, DTOs, services, parser test files |

## Register in Settings

Toolsets are **not** auto-discovered from script folders. Register each toolset explicitly:

1. Open the RevitDevTool panel → **Settings** → **MCP**
2. Add a toolset entry:
   - **.NET**: path to the built assembly (e.g. `RevitMcpToolSet.dll` after `dotnet build`)
   - **Python**: folder path containing a `*mcp.py` entry file
3. Save settings and wait for the host catalog to reload
4. Open the MCP registry view in the host UI — confirm tools, resources, and prompts appear
5. From your AI client, `search_dynamic` for a tool name (e.g. `revit_find_elements` or `get_demo_status`) and `invoke_dynamic` with the returned `capabilityId`

The MCP server `tools/list` still shows only infrastructure tools. Custom toolset tools appear only through `search_dynamic`.

## Build the .NET Samples

```powershell
dotnet build samples/McpToolsetDemo/McpToolsetDemo.csproj
dotnet build samples/RevitMcpToolSet/RevitMcpToolSet.csproj
```

Point **Settings → MCP** at the built DLL under `bin/`. See [MCP C# SDK](/docs/mcp/MCP-CSharp) for project setup and attribute patterns.

## Python Folder

Register the folder that contains a `*mcp.py` entry file. Dependencies belong in that entry file's PEP 723 header; `conftest.py` is not used for MCP registration. See [MCP Python SDK](/docs/mcp/MCP-Python) for the sample layout and `@mcp.tool` conventions. Wire names use snake_case only (no `Field(alias=)` on parameters).

## Run / Try

1. Build the .NET sample (if testing assembly-based discovery)
2. Register the assembly path or Python folder in **Settings → MCP**
3. Confirm entries in the host MCP registry UI
4. Confirm your AI client MCP config uses `--stdio` (see [MCP .NET](/docs/mcp/MCP-CSharp) or [MCP Python](/docs/mcp/MCP-Python))
5. In the AI client: `search_dynamic(query="<tool_name>")` → `invoke_dynamic(capabilityId=...)`
6. Call a read-only tool first (e.g. `get_demo_status`, `revit_get_model_summary`), then mutating tools

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Toolset not in registry UI | Path registered in Settings → MCP; .NET DLL built for correct host/API version |
| Python toolset not visible | Folder contains a `*mcp.py` file; its PEP 723 dependencies resolve successfully; the entry module imports without errors |
| `search_dynamic` finds nothing | Host running with add-in loaded; MCP pipe exists (`DevToolsMcp_*`); toolset registered and catalog reloaded |
| `invoke_dynamic` stale ID | Host disconnected or catalog changed — `search_dynamic` again, then invoke new `capabilityId` |
| Tool appears but call fails | Test a read-only tool first; check host document/selection preconditions |
| AI client cannot connect | MCP config points to `DevTools.Daemon.exe` with `args: ["--stdio"]`; .NET 10 installed |
| AI client connects but no hosts | At least one host open with RevitDevTool loaded; use `list_host_instances` |

## Related

- [MCP .NET](/docs/mcp/MCP-CSharp)
- [MCP Python](/docs/mcp/MCP-Python)
- [MCP C# SDK](/docs/mcp/MCP-CSharp)
- [MCP Python SDK](/docs/mcp/MCP-Python)
- [Modern Python Scripting](/docs/execution/python/Execution-Python)
- [TOOLSET-SPEC.md](https://github.com/trgiangv/RevitDevTool/blob/main/samples/TOOLSET-SPEC.md)
