# MCP Tools, Resources, and Prompts

## Daemon External Surface (Fixed)

Registered in `source/DevTools.Daemon/Mcp/Hosting/McpEngine.cs`. The daemon does **not**
project host tools/resources into `tools/list` / `resources/list`.
`ListChanged` is advertised as `false` for tools, prompts, and resources.

### Infrastructure Tools

| Tool | Assembly | Description |
|------|----------|-------------|
| `list_processes` | `DevTools.Daemon` | Lists connected instances + discovered MCP pipes |
| `launch_host` | `DevTools.Daemon` | Launches host (optional model file at startup; host inferred from extension when `filePath` is set) |
| `read_file_info` | `DevTools.Daemon` | On-disk Revit/DWG metadata reader |
| `list_machines` | `DevTools.Daemon` | Queries Gateway for connected devices (requires auth) |

### Host capabilities

| Tool | Assembly | Backing store | Purpose |
|------|----------|---------------|---------|
| `code_mode` | `DevTools.Daemon` | in-memory catalog, then the host pipe | C# method body. `SearchAsync` ranks the catalog. `InvokeAsync` and `ReadAsync` call the host. The model receives only the return value. |

Infrastructure tools and `code_mode` are registered together in `McpEngine.CreateLocalTools`.

`SearchAsync` does not open a host pipe. Arguments on the tool are `code` and `readOnly` (default false). There is no `id` argument.

**Agent payload knobs (daemon):**

| Tool | Parameter | Default | Purpose |
|------|-----------|---------|---------|
| `code_mode` | `readOnly` | `false` | When true, `InvokeAsync` throws unless `ReadOnlyHint` is true |
| `read_file_info` | `detail` | `summary` | `summary` = version/title/link names; `full` = complete on-disk metadata |

### Fixed Prompts (daemon-owned)

| Name | Arguments | Description |
|------|-----------|-------------|
| `revit_code` | `task`, optional `mode` | Generate `IExternalCommand` C# for Revit |
| `acad_code` | `task`, optional `mode` | Generate AutoCAD .NET command C# |

These are registered via SDK `PromptCollection` and answered entirely in-process.

---

## ProcessCatalogs

`ProcessSessions` connects to each `DevToolsMcp_*` pipe with an SDK `McpClient`
(`ProcessSession`), then lists tools/resources/templates into `ProcessCatalogs`
keyed by `processId`.

On host `list_changed` notifications, only that process slice is re-listed.
Disconnect removes the slice. External daemon `tools/list` is untouched.

Protocol `2026-07-28` delivers those notifications only on `subscriptions/listen`.
`ProcessSession` holds that request for the session (`toolsListChanged` and
`resourcesListChanged`) and waits for `notifications/subscriptions/acknowledged`
before the first catalog list. Initialize-handshake sessions (`2025-11-25` and
earlier) still receive the session-wide broadcast; they do not open
`subscriptions/listen`.

**Search ranking** (`ProcessCatalogs.Search`): see product contract
in `docs/product/mcp.md`. `SearchAsync` calls it from inside `code_mode`.

**Python dynamic toolsets** (`samples/PythonDemo/mcp_toolset`): tool function
parameters use snake_case wire names only (no `Field(alias=)` on params). Do not
register Python and C# toolsets that expose the same tool names simultaneously.

**C# sample templates** (`samples/RevitMcpToolSet`): `ElementResources` registers
`resource_template` entries `revit://element/{elementId}` (`application/json`) and
`revit://schedule/{scheduleId}/preview` (`text/csv`). Agents discover them via
`search_dynamic` with `kinds=["resource_template"]` and read via `invoke_dynamic`
(single `arguments` or batch `reads[]`).

---

## In-Host Tools

Registered via DI in host projects and exposed on the host SDK server
(`McpPipeServer` → per-connection `McpServer`). Host prompts are **not** registered.

### Built-in Tools (`IBuiltInMcpTool`)

| Tool | Host | Description |
|------|------|-------------|
| `execute_csharp_code` | Revit + AutoCAD | Compile and run C# with host API context |
| `execute_python_code` | Revit + AutoCAD | Execute inline Python with PEP 723 deps |
| `open_document` | Revit + AutoCAD | Open model file via `IDocumentBridge` |
| `navigate_history` | Revit + AutoCAD | Undo/redo navigation |
| `view_screenshot` | Revit + AutoCAD | Active viewport PNG (1280 px / 1280×720) as MCP image content |

### Built-in Resources (`IBuiltInMcpResource`)

| URI | Host | Description |
|-----|------|-------------|
| `revit://csharp-cheatsheet` | Revit | C# API patterns |
| `revit://python-cheatsheet` | Revit | Python API patterns |
| `revit://model/context` | Revit | Live model state |
| `revit://model/warnings` | Revit | Active warnings |
| `revit://version` | Revit | Host/runtime version |
| `acad://csharp-cheatsheet` | AutoCAD | C# API patterns |
| `acad://python-cheatsheet` | AutoCAD | Python API patterns |

### Dynamic Tools (User-Registered)

Loaded from user-configured paths via `McpCatalogStore`:

- **.NET assemblies** — `[McpTool]` via `DotnetMcpAssemblyParser`
- **Python toolsets** — `PythonToolsetParser` / `ToolParser.py`

---

## Schema Ownership

| Surface | How InputSchema is produced |
|---------|-----------------------------|
| Daemon tools | SDK `McpServerTool.Create(handler)` — schema from method signature + `[Description]` |
| Host built-in tools (`IBuiltInMcpTool`) | `DescriptorFactory` + SDK tool collection |
| Discovered .NET assemblies (`McpAssemblyParser`) | `McpSchemaBuilder.FromClrType` over MetadataLoadContext parameters |

`JsonSchemaObject` / `JsonSchemaProperty` under `DevTools.Mcp.Catalog` are for **parsing** schemas (UI only).

---

## Call Logging

MCP call observability is **always-on** at protocol boundaries — not optional and not a separate tool.
Serialization uses **`McpJsonUtilities.DefaultOptions`** across daemon and host filters.

| Surface | Attachment | Logger category |
|---------|------------|-----------------|
| Daemon `tools/call` | SDK `CallToolFilters` via `McpServerConfigurator` | `DevTools.Mcp.ToolCall` |
| Host `tools/call` / `resources/read` | `McpLogFilters` on host `McpServerOptions` | `DevTools.Mcp.ToolCall` / `DevTools.Mcp.ResourceRead` |

Each call emits a single compact ZLogger line. Dynamic daemon handlers use method string
`tools/call` (the protocol operation they represent), not a separate `dynamic.*` prefix:

```text
tools/call ok target=execute_csharp_code durationMs=718 args={"code":"..."} result={...}
resources/read ok target=revit://model/context durationMs=42 result={...}
tools/call ok target=search_dynamic durationMs=3 args={"query":"wall",...} result={...}
```

| Token | Meaning |
|-------|---------|
| `tools/call` / `resources/read` | MCP method (daemon dynamic tools log as `tools/call`) |
| `ok` / `error` / `timeout` | Outcome |
| `target` | Tool name or resource URI |
| `args` | Protocol JSON arguments (`JsonElement` dictionary via `McpJsonUtilities`) |
| `result` | Serialized `CallToolResult` / `ReadResourceResult` via `McpJsonUtilities` |
| `durationMs` | Wall time |

**Binary content:** when a result includes image/audio blocks, monitor lines omit base64
`Data` and log `type`, `mimeType`, and `length` only (SDK DebuggerDisplay spirit).

`search_dynamic` monitor lines log `count` and compact hit summaries — not the full catalog JSON returned to the client.

`ProcessSessions` catalog refresh uses structured ZLogger scopes (counts + duration) — no custom
`ActivitySource`. Protocol MCP spans stay with the SDK.
Host SDK protocol chatter (`ModelContextProtocol.*`) is filtered to `Warning` so call logs stay visible.

### MCP Tasks (SDK `Extensions.Tasks`)

`AddMcp()` on the host registers `WithTasks` with the SDK default selector.
The external client talks only to the Daemon. `DevTools.Daemon/Mcp/TaskSelection.cs`
is `McpTasksOptions.ExecutionModeSelector`:

| Call | Mode |
|------|------|
| `launch_host` | **Optional** |
| `invoke_dynamic` of a catalog tool | **Optional** |
| Resource read, `reads` batch, other infrastructure tools | **Synchronous** |

**Required** is unused until clients advertise `io.modelcontextprotocol/tasks`. Daemon-to-host routing stays synchronous `ProcessSession.CallToolPassthroughAsync` (task polling is client ↔ daemon only).

### ResourceLink pass-through

Host and catalog tool responses may include SDK `ResourceLinkBlock` content (URI + metadata,
no inline payload). `code_mode` returns that block when the program returns it.
Clients that support resource links can resolve URIs themselves; unsupported clients skip the block.

### Structured output (SDK 2.0)

Fixed server tools (`list_processes`, `read_file_info`) emit
`StructuredContent` manually via `ToolResults` and **do not** set
`UseStructuredContent` on `tools/list` yet — auto `outputSchema` from `JsonElement`
members breaks strict clients (Cursor drops the entire tool list). Host toolsets may
use `UseStructuredContent` where schemas are stable. Structured host tools should
return a domain DTO and let the SDK create `CallToolResult`; direct result construction
is reserved for custom content blocks (images, links, or elicitation). `code_mode`
passes a host `CallToolResult` through when the program returns it.

### JSON policy

| Role | Serializer |
|------|------------|
| Protocol wire, logs, discovery, tool result text | `ToolHelpers.Serialize` / `ToolHelpers.ToElement` (wraps `McpJsonUtilities.DefaultOptions`) |

See [MCP README](README.md) for dual-server vocabulary and DI lifecycle.

### `revit://model/context` collector strategy

Element counts use per-category `FilteredElementCollector.GetElementCount()` — native
count-only queries that do not hydrate elements. `ElementMulticategoryFilter` + iterate
is reserved for cases that need element data, not count-only snapshots.

There is **no TTL cache** on this resource: counts must reflect live document state after
mutations. Latency targets are validated via `docs/agents/mcp-integration-test.md`
(3× sequential read on Snowdon Towers; warm `durationMs` &lt; 500).

Implementation: `source/DevTools.Mcp.Revit/Resources/RevitModelContext.cs`.

### File logs

See [daemon.md](daemon.md) (`Logging:File` → `%APPDATA%/RevitDevTool/logs/`).

---

## Source Map

| Area | Path |
|------|------|
| Daemon infra + dynamic tools + prompts | `source/DevTools.Daemon/Mcp/` |
| Daemon file logging | `source/DevTools.Daemon/Composition/FileLogging.cs` |
| Call-log payload helpers | `source/DevTools.Mcp.Catalog/Hosting/McpLogPayload.cs` |
| Shared MCP hosting (Tasks, filters) | `source/DevTools.Daemon/Mcp/Hosting/McpServerConfigurator.cs`, `DevTools.Mcp.Catalog/Hosting/McpLogFilters.cs` |
| Tool result helpers | `source/DevTools.Mcp.Catalog/Core/Utils/ToolHelpers.cs` |
| Host pipe server | `source/DevTools.Mcp.Catalog/Transport/McpPipeServer.cs` |
| Schema builder (discovery) | `source/DevTools.Mcp.Catalog/Discovery/McpSchemaBuilder.cs` |
| Schema parse models (UI) | `source/DevTools.Mcp.Catalog/JsonSchemaModels.cs` |
| Daemon process catalog index | `source/DevTools.Daemon/Mcp/Processes/ProcessCatalogs.cs` |
| In-host catalog | `source/DevTools.Mcp.Catalog/McpCatalogStore.cs` |
| Built-in host tools / resources | `source/DevTools.Mcp.Revit/`, `source/DevTools.Mcp.Acad/` |
| Built-in execution tools | `source/DevTools.Execution/External/Mcp/BuiltIn/` |
