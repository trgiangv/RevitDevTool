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

## Load a toolset

Toolsets are **not** auto-discovered from script folders. Load each toolset explicitly from the **MCP** toggle:

1. Open the RevitDevTool panel → **MCP** toggle
2. Load a toolset:
   - **Load .NET Toolset**: the built assembly (e.g. `RevitMcpToolSet.dll` after `dotnet build`)
   - **Load Python Toolset**: the folder containing a `*mcp.py` entry file
3. Wait for the host catalog to reload
4. Confirm tools, resources, and prompts appear in the registry
5. From your AI client, ask it to find and call the tool by name (e.g. `revit_find_elements` or `get_demo_status`); the client does this through `code_mode`

The MCP server `tools/list` stays fixed (`list_processes`, `launch_host`, `read_file_info`, `code_mode`). Custom toolset tools are reached only through `code_mode`.

## Build the .NET Samples

```powershell
dotnet build samples/McpToolsetDemo/McpToolsetDemo.csproj
dotnet build samples/RevitMcpToolSet/RevitMcpToolSet.csproj
```

Load the built DLL under `bin/` with **Load .NET Toolset**. See [MCP .NET](/docs/mcp/MCP-CSharp) for project setup and attribute patterns.

## Python Folder

Load the folder that contains a `*mcp.py` entry file. Dependencies belong in that entry file's PEP 723 header; `conftest.py` is not used for MCP loading. See [MCP Python](/docs/mcp/MCP-Python) for the sample layout and `@mcp.tool` conventions. Wire names use snake_case only (no `Field(alias=)` on parameters).

## Run / Try

1. Build the .NET sample (if testing assembly-based discovery)
2. Load the assembly or Python folder from the **MCP** view
3. Confirm entries in the **MCP** view
4. Confirm your AI client MCP config uses `--stdio` (see [MCP .NET](/docs/mcp/MCP-CSharp) or [MCP Python](/docs/mcp/MCP-Python))
5. In the AI client: ask it to search for `<tool_name>` and call it through `code_mode`
6. Call a read-only tool first (e.g. `get_demo_status`, `revit_get_model_summary`), then mutating tools

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Toolset not in the MCP view | Toolset loaded from the MCP view (listed under **Settings → MCP**); .NET DLL built for correct host/API version |
| Python toolset not visible | Folder contains a `*mcp.py` file; its PEP 723 dependencies resolve successfully; the entry module imports without errors |
| `code_mode` search finds nothing | Host running with the add-in loaded; toolset registered and catalog reloaded |
| Call fails with a stale capability | Host disconnected or catalog changed — search again inside `code_mode`, then call the new result |
| Tool appears but call fails | Test a read-only tool first; check host document/selection preconditions |
| AI client cannot connect | MCP config points to `DevTools.Daemon.exe` with `args: ["--stdio"]`; .NET 10 installed |
| AI client connects but no hosts | At least one host open with RevitDevTool loaded; call `list_processes` |

## Related

- [MCP .NET](/docs/mcp/MCP-CSharp)
- [MCP Python](/docs/mcp/MCP-Python)
- [Modern Python Scripting](/docs/execution/python/Execution-Python)
- [TOOLSET-SPEC.md](https://github.com/trgiangv/RevitDevTool/blob/main/samples/TOOLSET-SPEC.md)
