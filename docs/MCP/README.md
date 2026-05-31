# MCP Integration Architecture

Model Context Protocol integration lets external AI clients (Claude Desktop, Cursor, etc.) talk to running host applications through a standalone MCP server and the in-host named-pipe runtime.

The entire MCP stack is designed to be host-agnostic. The standalone `MCPServer.exe` discovers and connects to any host pipe (`Revit_*`, `AutoCad_*`, `Civil3D_*`, etc.) via generic `HostBridgeClient`. The in-host runtime in `DevTools.Execution` uses `IHostAppInfo` and host adapters. Built-in standalone tools now support both Revit and AutoCAD-family products (launch, file info, instance discovery).

Last updated: 2026-05-31

---

## Source Map

| Area | Path |
|------|------|
| Parser/contracts | `source/DevTools.McpParser/` |
| Standalone MCP server | `source/DevTools.McpServer/` |
| In-host runtime | `source/DevTools.Execution/External/Mcp/` |
| Pipe server | `source/DevTools.Execution/External/DevToolsPipeServer.cs` |
| Registry UI | `source/DevTools.Presentation/ViewModels/McpRegistryViewModel.cs` |
| Python parser script | `source/DevTools.Execution/Resources/scripts/ToolParser.py` |

---

## Runtime Shape

```mermaid
flowchart TB
    Client["MCP client\nClaude Desktop / Cursor"]
    Server["DevTools.McpServer\nstandalone MCPServer.exe"]
    Discovery["InstanceManager\nscan \\.\pipe\\ for host pipes"]

    subgraph hosts["Host processes"]
        RevitPipe["DevToolsPipeServer\nRevit_2025_pid"]
        AcadPipe["DevToolsPipeServer\nAutoCad_2026_pid"]
    end

    Registry["ToolRegistryStore"]
    Providers["DotnetToolRegistryProvider\nPythonToolRegistryProvider"]
    Dispatch["Tool/Prompt/Resource dispatchers"]
    Host["Host context + Python executor"]

    Client --> Server
    Server --> Discovery
    Discovery -->|"HostBridgeClient"| RevitPipe
    Discovery -->|"HostBridgeClient"| AcadPipe
    RevitPipe --> Registry
    AcadPipe --> Registry
    Registry --> Providers
    RevitPipe --> Dispatch
    AcadPipe --> Dispatch
    Dispatch --> Host
```

The standalone MCP server owns MCP protocol routing and host instance selection. `InstanceManager` scans `\\.\pipe\` for pipes matching `{HostApp}_{Version}_{PID}` and connects via generic `HostBridgeClient`. The host process owns actual execution, registry loading, and host-safe invocation.

---

## Parser Library

`source/DevTools.McpParser/` contains shared bridge and registry contracts:

- `Models/BridgeMessage.cs`, `BridgeMethods.cs`, `BridgePipeConnection.cs`
- `Models/McpRegisteredTool.cs`, `McpRegisteredPrompt.cs`, `McpRegisteredResource.cs`
- `Models/McpRegistryCatalog.cs`
- `Dotnet/DotnetMcpAssemblyParser.cs`
- `Python/PythonToolsetParser.cs`
- `RequestContextFactory.cs`

This library is shared by the standalone MCP server, in-host runtime, and tests.

---

## Registry Flow

```mermaid
sequenceDiagram
    participant UI as Registry UI
    participant Store as ToolRegistryStore
    participant Loader as ToolRegistryCatalogLoader
    participant Dotnet as DotnetToolRegistryProvider
    participant Python as PythonToolRegistryProvider
    participant Settings as ISettingsService

    UI->>Store: AddPathAsync / ReloadAsync
    Store->>Settings: Read configured paths
    Store->>Loader: LoadCatalog(dotnetPaths, pythonPaths)
    Loader->>Dotnet: Parse assemblies
    Loader->>Python: Parse toolset directories
    Python->>Python: Pre-resolve dependencies for MCP entry files
    Loader-->>Store: McpRegistryCatalog
    Store->>Settings: Persist accepted paths and prune invalid paths
    Store-->>UI: ToolsChanged
```

`.NET` catalogs are parsed from assemblies. Python catalogs are parsed from directories through `ToolParser.py` and in-process Python execution.

---

## Dispatch Flow

`RegistryRequestHandler` handles pipe methods:

- `tools/list`
- `tools/call`
- `prompts/list`
- `prompts/get`
- `resources/list`
- `resources/templates/list`
- `resources/read`

Dispatchers split by primitive:

| Dispatcher | .NET path | Python path |
|------------|-----------|-------------|
| `ToolExecutionDispatcher` | `DotnetMcpServerFactory` creates/caches `McpServerTool` wrappers | `PythonExecutor` invokes the Python binding |
| `PromptExecutionDispatcher` | `McpServerPrompt` wrapper | Python prompt binding |
| `ResourceExecutionDispatcher` | `McpServerResource` wrapper | Python resource binding |

`McpPrimitiveBinding.CreatePrimitiveId()` normalizes IDs for stable lookup and duplicate handling.

---

## Standalone Server

`source/DevTools.McpServer/` contains:

- `Program.cs` — MCP stdio server entry, tool/prompt/resource routing
- `CatalogService.cs` — aggregates tool catalogs from connected hosts
- `InstanceManager.cs` — discovers host pipes, manages `HostBridgeClient` connections
- `HostBridgeClient.cs` — generic named-pipe client (formerly `RevitBridgeClient`)
- Routing primitives: `RoutingMcpServerTool`, `RoutingMcpServerPrompt`, `RoutingMcpServerResource`

### Built-in Standalone Tools

Registered in `source/DevTools.McpServer/Program.cs`:

| Tool | Host Support | Description |
|------|-------------|-------------|
| `list_host_instances` | All hosts | Lists connected instances + discovered pipes |
| `launch_host` | Revit + AutoCAD family | Launches host with optional file path; supports AutoCad, Civil3D, Plant3D, etc. |
| `read_file_info` | Revit (RVT/RFA) + AutoCAD (DWG) | Offline metadata reader via compound-file and ACadSharp |
| `open_model` | Multi-host | Extension-based host detection; on a connected instance routes `open_document` via pipe; on cold start launches host with file as CLI arg |

### Built-in In-Host Tools

Registered in `source/DevTools.Execution/External/Mcp/BuiltIn/`:

| Tool | Host Support | Description |
|------|-------------|-------------|
| `execute_csharp_code` | Revit + AutoCAD | Compiles and runs C# code with host API context |
| `open_document` | Revit + AutoCAD | Opens a model file via `IDocumentBridge` (`RevitDocumentBridge` / `AcadDocumentBridge`) |

### Host-Specific Gaps

- No shipped AutoCAD MCP toolset equivalent to `Samples/RevitMcpToolSet/`
- Navisworks listed in enums but launch returns "not yet supported"

---

## Pytest Routes

`DevToolsPipeServer` also routes `tests/discover` and `tests/run` to the pytest bridge. Those routes are documented in `docs/PyTest/README.md`.

---

## Verification Reality

Current MCP tests mostly cover parser and contract shapes. They do not deeply prove live named-pipe dispatch, host threading, or end-to-end MCP client behavior.

When changing MCP behavior:

- Add focused parser/contract tests for schema or identity changes.
- Build the host that owns the changed runtime.
- State live-host or named-pipe verification gaps when they cannot be run.

---

## Related Docs

- `docs/ai/mcp-pytest-bridge.md`
- `docs/Execution/README.md`
- `docs/PyTest/README.md`
- `Samples/McpToolsetDemo/`
- `Samples/PythonDemo/mcp_toolset/`
- `Samples/RevitMcpToolSet/`
