# MCP Dispatch: In-Host Primitive Execution

## Overview

MCP (Model Context Protocol) tools, prompts, and resources execute inside the host process via `McpBridgeRequestHandler`. The handler routes JSON-RPC requests from the Named Pipe to the appropriate catalog entry and dispatches execution on the host thread.

---

## Source Map

| File | Role |
|------|------|
| `source/DevTools.Mcp/Handlers/McpBridgeRequestHandler.cs` | Bridge request handler (tools, prompts, resources) |
| `source/DevTools.Mcp/Dispatch/IMcpPrimitiveDispatcher.cs` | Dispatch interface (tool/prompt/resource) |
| `source/DevTools.Mcp/Dispatch/IMcpExecutionTracker.cs` | Execution state tracking interface |
| `source/DevTools.Mcp/Registry/McpCatalogStore.cs` | Tool/prompt/resource registry |
| `source/DevTools.Execution/External/Mcp/Dispatchers/McpPrimitiveDispatcher.cs` | Dispatch implementation |
| `source/DevTools.Execution/External/Mcp/BuiltIn/CSharpCodeTool.cs` | Built-in: `execute_csharp_code` |
| `source/DevTools.Execution/External/Mcp/BuiltIn/OpenDocumentTool.cs` | Built-in: `open_document` |

---

## Execution Flow

```mermaid
sequenceDiagram
    participant Client as AI Client (Daemon/Cursor)
    participant Pipe as Named Pipe (DevToolsPipeServer)
    participant Handler as McpBridgeRequestHandler
    participant Catalog as McpCatalogStore
    participant Dispatcher as McpPrimitiveDispatcher
    participant Host as IHostContextExecutor
    participant Guard as ExecutionGuard

    Client->>Pipe: tools/call {name, arguments}
    Pipe->>Handler: HandleToolsCallAsync()
    Handler->>Handler: Set ExecutionGuardContext.Mode = Suppress
    Handler->>Catalog: TryGetTool(name)
    Handler->>Dispatcher: DispatchToolAsync(tool, payload, hostContext)
    Dispatcher->>Host: ExecuteAsync(handler)
    Note over Guard: Guard wraps execution (suppress mode)
    Host-->>Dispatcher: McpToolExecutionResult
    Dispatcher-->>Handler: Result
    Handler-->>Pipe: BridgeMessage.Response
    Pipe-->>Client: JSON result
```

---

## Supported Routes

| Method | Handler | Notes |
|--------|---------|-------|
| `tools/list` | `HandleToolsListAsync` | Returns all registered tools |
| `tools/call` | `HandleToolsCallAsync` | Executes tool with guard suppression |
| `prompts/list` | `HandlePromptsListAsync` | Returns all prompts |
| `prompts/get` | `HandlePromptsGetAsync` | Executes prompt with guard suppression |
| `resources/list` | `HandleResourcesListAsync` | Returns direct resources |
| `resources/templates/list` | `HandleResourceTemplatesListAsync` | Returns resource templates |
| `resources/read` | `HandleResourcesReadAsync` | Reads resource with guard suppression |

---

## Tool Source Kinds

Tools come from different sources, all unified through the catalog:

| Source | Description |
|--------|-------------|
| .NET Assembly | Reflected from `[McpTool]` attribute on methods in `.dll` toolsets |
| Python | Parsed from `ToolParser.py` analysis of Python toolset scripts |
| Built-in C# | Hardcoded tools: `execute_csharp_code`, `open_document` |

---

## Execution Guard Integration

All MCP dispatch paths set `ExecutionGuardContext.Mode = Suppress` before calling `hostContext.ExecuteAsync()`. This ensures:
- AI tool calls never hang on unexpected dialogs
- Transaction failures are auto-resolved or rolled back
- Rollback info is available via `ExecutionGuardContext.RollbackSummary`

---

## Pipe Server

`DevToolsPipeServer` is registered as `IHostedService` by `ExecutionExtensions.AddExecutionServices()`.

Pipe name format: `DevTools_{Host}_{VersionNumber}_{ProcessId}`

Examples:
- `DevTools_Revit_2025_12345`
- `DevTools_AutoCad_2026_7890`

---

## Timeout

All tool/prompt/resource calls have a 120-second timeout (`CallTimeout`). On timeout, the handler returns a structured error with code `tool_invoke_failed`.

---

## Related

- MCP protocol docs: `docs/MCP/README.md`
- Transport & daemon: `docs/MCP/transport.md`, `docs/MCP/daemon.md`
- Agent digest: `docs/agents/mcp-pytest-bridge.md`
