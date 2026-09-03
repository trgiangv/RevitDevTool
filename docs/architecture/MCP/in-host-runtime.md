# In-Host MCP Runtime

The in-host runtime runs inside Revit/AutoCAD and handles actual tool/resource execution
behind a spec-first MCP handler on `DevToolsMcp_*` (protocol `2026-07-28`).

## Runtime Shape

```mermaid
flowchart TB
    Daemon["DevTools.Daemon"]
    Broker["HostBroker<br/>scan DevToolsMcp_*"]
    Catalog["ConnectedHostCatalog"]

    subgraph hosts["Host processes"]
        McpPipe["HostMcpPipeServer<br/>DevToolsMcp_Revit_2025_pid"]
        PytestPipe["DevToolsPipeServer<br/>DevTools_Revit_2025_pid<br/>(pytest/control only)"]
    end

    Registry["McpCatalogStore"]
    Providers["DotnetMcpRegistryProvider<br/>PythonToolRegistryProvider"]
    Dispatch["McpPrimitiveDispatcher"]
    Host["Host context + Python executor"]

    Daemon --> Broker
    Broker --> Catalog
    Broker -->|"SDK McpClient"| McpPipe
    McpPipe --> Registry
    Registry --> Providers
    McpPipe --> Dispatch
    Dispatch --> Host
    Pytest["pytest client"] --> PytestPipe
```

The Daemon owns external MCP protocol routing and host selection. `HostBroker`
scans for `DevToolsMcp_{Host}_{Version}_{PID}` whose PID is still a live process, connects with `StreamClientTransport`,
and hydrates `ConnectedHostCatalog`. The host process owns execution, registry loading, and
host-safe invocation. Pytest/control remains on the `DevTools_*` pipe.

## Registry Flow

```mermaid
sequenceDiagram
    participant UI as Registry UI
    participant Store as McpCatalogStore
    participant Loader as McpCatalogLoader
    participant Dotnet as DotnetMcpRegistryProvider
    participant Python as PythonToolRegistryProvider
    participant Settings as ISettingsService
    participant HostServer as HostMcpPipeServer

    UI->>Store: EnsureLoaded (init) / AddPathAsync / ReloadAsync
    Store->>Settings: Read configured paths
    Store->>Loader: LoadCatalog(dotnetPaths, pythonPaths)
    Loader->>Dotnet: Parse assemblies
    Loader->>Python: Parse toolset directories
    Loader-->>Store: McpRegistryCatalog
    Store->>Settings: Persist accepted paths
    Note over Store,HostServer: CatalogChanged only when tool/resource IDs change
    Store-->>HostServer: CatalogChanged
    HostServer->>HostServer: Invalidate cached primitive lists
    HostServer-->>HostServer: tools/list_changed notifications
```

## Dispatch Flow

`HostMcpPipeServer` delegates JSON-RPC to `McpHandler`, which encodes list/read/call
responses from `McpCatalogStore` and `IMcpPrimitiveDispatcher` on the host thread.
Cancellation is the request token; tool-internal timeouts stay on the tool.

Host prompts are not registered; guidance lives in daemon fixed prompts.

| Primitive | .NET path | Python path |
|-----------|-----------|-------------|
| Tool call (built-in) | `IBuiltInMcpTool` via dispatcher | `PythonExecutor` binding |
| Tool call (.NET toolset, ALC) | `ToolsetInvoker` + `ToolsetResultSerializer` JSON bridge | — |
| Resource read | Dispatcher resource path | Python resource binding |

See [Platform boundaries](platform-boundaries.md) for ALC and MRTR detail.

## Parser Library

MCP contracts live in `source/DevTools.Mcp.Core/`; catalog/discovery lives in `DevTools.Mcp.Catalog/`; the daemon-side capability index is in `DevTools.Mcp.Client/`; and SDK host adapters, including `HostMcpPipeServer`, are in `DevTools.Mcp.Adapter/`. `source/DevTools.Ipc/` owns `BridgeMessage.cs`, `BridgePipeConnection.cs`, `HostPipeName.cs`, and the dual pipe prefixes. Wire-format property names belong in Core's `McpSpecKeys` (`DevTools.Mcp.Core/Protocol/`). Daemon-specific contract keys live in `DaemonPropertyNames`.
