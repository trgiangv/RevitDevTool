# MCP Tools

## Built-in Daemon Tools

Registered in `source/DevTools.Daemon/Mcp/McpEngine.cs`:

| Tool | Host Support | Description |
|------|-------------|-------------|
| `list_host_instances` | All hosts | Lists connected instances + discovered pipes |
| `launch_host` | Revit + AutoCAD family | Launches host with optional file path |
| `read_file_info` | Revit (RVT/RFA) + AutoCAD (DWG) | Offline metadata reader |
| `open_model` | Multi-host | Extension-based host detection + launch |
| `list_dynamic_tools` | All hosts | Lists dynamic tools per instance |
| `call_dynamic_tool` | All hosts | Calls a dynamic tool on its owning instance |
| `refresh_dynamic_catalog` | All hosts | Re-queries all instances |
| `list_machines` | Gateway only | Queries Gateway `/machines` endpoint for connected devices |

## Built-in In-Host Tools

Registered in `source/DevTools.Execution/External/Mcp/BuiltIn/`:

| Tool | Host Support | Description |
|------|-------------|-------------|
| `execute_csharp_code` | Revit + AutoCAD | Compiles and runs C# code with host API context |
| `open_document` | Revit + AutoCAD | Opens a model file via `IDocumentBridge` |

## Dynamic Tools (User-Registered)

Dynamic tools are loaded from user-configured paths via `ToolRegistryStore`:

- **.NET assemblies** — parsed by `DotnetMcpAssemblyParser`
- **Python toolsets** — parsed by `PythonToolsetParser` via `ToolParser.py`

Dynamic registrations are tracked per host instance. A tool registered in one Revit process is not assumed to exist in another.

## Host-Specific Gaps

- No shipped AutoCAD MCP toolset equivalent to `Samples/RevitMcpToolSet/`
- Navisworks listed in enums but launch returns "not yet supported"
