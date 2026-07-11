# MCP Tools, Resources, and Prompts

## Daemon-Level Primitives

Registered in `source/DevTools.Daemon/Mcp/McpEngine.cs`. Available regardless of host connection.

### Infrastructure Tools

| Tool | Description |
|------|-------------|
| `list_host_instances` | Lists connected instances + discovered pipes |
| `launch_host` | Launches host (Revit, AutoCAD family) with optional file path |
| `read_file_info` | Offline Revit/DWG metadata reader |
| `open_model` | Extension-based host detection + file open |
| `list_machines` | Queries Gateway for connected devices (requires auth) |

### Symmetric Catalog Tools

Each MCP primitive type has a consistent List + Invoke pair backed by a catalog:

```
DynamicXxxCatalog.Resolve(key, hostInstanceId?) → Found | NotFound | Ambiguous
```

| Tool | Catalog | Purpose |
|------|---------|---------|
| `list_dynamic_tools` | `DynamicToolCatalog` | List tools grouped by name per instance |
| `call_dynamic_tool` | `DynamicToolCatalog` | Execute a tool (with multi-instance resolution) |
| `list_dynamic_resources` | `DynamicResourceCatalog` | List resources grouped by URI per instance |
| `read_dynamic_resource` | `DynamicResourceCatalog` | Read a resource (text or blob) |
| `list_dynamic_prompts` | `DynamicPromptCatalog` | List prompts grouped by name per instance |
| `get_dynamic_prompt` | `DynamicPromptCatalog` | Get prompt content |
| `refresh_dynamic_catalog` | All three | Re-fetch all primitives from connected hosts |

**Multi-instance resolution**: When multiple instances provide the same key, the caller must specify `hostInstanceId` (PID). Without it, single-instance resolves automatically; multiple returns `Ambiguous` with candidate PIDs.

---

## In-Host Tools

Registered via DI in host projects (e.g., `RevitHostingExtensions.AddExecutionServices()`).

### Built-in Tools (`IBuiltInMcpTool`)

| Tool | Host | Description |
|------|------|-------------|
| `execute_csharp_code` | Revit + AutoCAD | Compile and run C# `IExternalCommand` with host API context |
| `open_document` | Revit + AutoCAD | Open model file via `IDocumentBridge` |
| `undo_changes` | Revit | Undo N transactions via `RevitTransactionService` |

### Built-in Resources (`IBuiltInMcpResource`)

| URI | Host | Description |
|-----|------|-------------|
| `revit://api-cheatsheet` | Revit | API patterns, usings, version pitfalls |
| `revit://model/context` | Revit | Live model state: levels, categories, units, active view |
| `revit://model/warnings` | Revit | Active constraint violations and element IDs |
| `revit://version` | Revit | Host version, .NET runtime, API compatibility notes |
| `revit://view/screenshot` | Revit | Active view as 1920px PNG (BlobResourceContents) |

### Built-in Prompts (`IBuiltInMcpPrompt`)

| Name | Host | Arguments | Description |
|------|------|-----------|-------------|
| `revit_code` | Revit | `task` (required), `mode` (optional) | Generate complete `IExternalCommand` C# code |

---

## Dynamic Tools (User-Registered)

Loaded from user-configured paths via `McpCatalogStore`:

- **.NET assemblies** — parsed by `DotnetMcpAssemblyParser` for `[McpTool]` attributes
- **Python toolsets** — parsed by `PythonToolsetParser` via `ToolParser.py`

Dynamic registrations are tracked per host instance.

---

## Routing Models

### Protocol-Native (Standard MCP)

`CatalogService` builds flat `tools/list`, `resources/list`, `prompts/list` surfaces from all connected hosts. Collision resolution:

- Tools: namespace as `{toolName}@{hostApp}_{version}`
- Resources: deduplicated by URI
- Prompts: namespace as `{promptName}@{hostApp}_{version}`

This is the path standard MCP clients (Cursor, Claude Desktop) use directly.

### Tool-Based Relay (Dynamic Catalog)

The `list_dynamic_*` + `call/read/get_dynamic_*` tools provide explicit PID-based routing for:

- Clients that need fine-grained multi-instance control
- Diagnostic/admin workflows
- Clients that don't natively surface resources/prompts in UI

Both paths coexist — protocol-native works automatically, tool-based relay is available when needed.

---

## Source Map

| Area | Path |
|------|------|
| Daemon tools | `source/DevTools.Daemon/Mcp/Tools/` |
| Catalogs | `source/DevTools.Mcp/Routing/Catalog/` |
| Catalog service | `source/DevTools.Mcp/Routing/Catalog/CatalogService.cs` |
| Built-in tools | `source/DevTools.Execution/External/Mcp/BuiltIn/` |
| Revit agents | `source/DevTools.Agents.Revit/` |
| AutoCAD agents | `source/DevTools.Agents.Acad/` |
