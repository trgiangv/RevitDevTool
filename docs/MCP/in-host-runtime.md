# In-Host MCP Runtime

The in-host runtime runs inside Revit/AutoCAD and handles actual tool execution.

## Runtime Shape

```mermaid
flowchart TB
    Daemon["DevTools.Daemon"]
    Discovery["InstanceManager<br/>scan \\.\pipe\\ for host pipes"]

    subgraph hosts["Host processes"]
        RevitPipe["DevToolsPipeServer<br/>Revit_2025_pid"]
        AcadPipe["DevToolsPipeServer<br/>AutoCad_2026_pid"]
    end

    Registry["ToolRegistryStore"]
    Providers["DotnetToolRegistryProvider<br/>PythonToolRegistryProvider"]
    Dispatch["Tool/Prompt/Resource dispatchers"]
    Host["Host context + Python executor"]

    Daemon --> Discovery
    Discovery -->|"HostBridgeClient"| RevitPipe
    Discovery -->|"HostBridgeClient"| AcadPipe
    RevitPipe --> Registry
    AcadPipe --> Registry
    Registry --> Providers
    RevitPipe --> Dispatch
    AcadPipe --> Dispatch
    Dispatch --> Host
```

The Daemon owns MCP protocol routing and host instance selection. `InstanceManager` scans `\\.\pipe\` for pipes matching `{HostApp}_{Version}_{PID}` and connects via generic `HostBridgeClient`. The host process owns actual execution, registry loading, and host-safe invocation.

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

## Dispatch Flow

`RegistryRequestHandler` handles pipe methods:

- `tools/list`, `tools/call`
- `prompts/list`, `prompts/get`
- `resources/list`, `resources/templates/list`, `resources/read`

Dispatchers split by primitive:

| Dispatcher | .NET path | Python path |
|------------|-----------|-------------|
| `ToolExecutionDispatcher` | `DotnetMcpServerFactory` creates/caches `McpServerTool` wrappers | `PythonExecutor` invokes the Python binding |
| `PromptExecutionDispatcher` | `McpServerPrompt` wrapper | Python prompt binding |
| `ResourceExecutionDispatcher` | `McpServerResource` wrapper | Python resource binding |

`McpPrimitiveBinding.CreatePrimitiveId()` normalizes IDs for stable lookup and duplicate handling.

## Parser Library

`source/DevTools.McpParser/` contains shared bridge and registry contracts:

- `Models/BridgeMessage.cs`, `BridgeMethods.cs`, `BridgePipeConnection.cs`
- `Models/Constants.cs` — canonical property names and type values
- `Models/McpRegisteredTool.cs`, `McpRegisteredPrompt.cs`, `McpRegisteredResource.cs`
- `Models/McpRegistryCatalog.cs`
- `Dotnet/DotnetMcpAssemblyParser.cs`
- `Python/PythonToolsetParser.cs`
- `RequestContextFactory.cs`

Wire-format property names belong in `McpPropertyNames`; do not duplicate in other projects.
