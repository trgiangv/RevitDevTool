# MCP Integration Architecture

Model Context Protocol (MCP) integration enables external tools (Claude Desktop, VS Code extensions) to interact with Revit through a standardized protocol. Split across three source projects plus the add-in host.

---

## Architecture Overview

```mermaid
flowchart TB
    Client["External MCP Client\n(Claude Desktop, VS Code)"]
    
    subgraph PipeServer["DevToolsPipeServer (in Revit)"]
        direction LR
        R1["tools/list"]
        R2["tools/call"]
        R3["prompts/list"]
        R4["resources/list"]
        R5["resources/read"]
        R6["instance/info"]
        R7["tests/discover"]
        R8["tests/run"]
    end

    subgraph Backend["Backends"]
        Registry["ToolRegistryStore\n(Discovery + Cache)"]
        Pytest["PytestExecutionService\n(Python scope)"]
    end

    subgraph Providers["Tool Providers"]
        DotNet["DotnetToolRegistryProvider\n(.NET tools from assemblies)"]
        Python["PythonToolRegistryProvider\n(Python tools from directories)"]
    end

    Client -->|"Named Pipe (IPC)"| PipeServer
    R1 --> Registry
    R2 --> Registry
    R3 --> Registry
    R4 --> Registry
    R5 --> Registry
    R6 -->|"InstanceRequestHandler"| Client
    R7 --> Pytest
    R8 --> Pytest
    Registry --> DotNet
    Registry --> Python
```

---

## Source Projects

### DevTools.McpParser (`source/DevTools.McpParser/`)

Message parsing library shared by both server and client:
- **Models/** — Bridge message types, pipe connection protocol
- **Dotnet/** — .NET tool parser (attribute-based discovery)
- **Python/** — Python tool parser (annotation-based discovery)
- **RequestContextFactory.cs** — Context factory for request handling

### DevTools.McpServer (`source/DevTools.McpServer/`)

Standalone MCP server process (publishable binary):
- **Program.cs** — Main entry point
- **RoutingMcpServerTool/Prompt/Resource** — MCP routing handlers
- **RevitBridgeClient.cs** — Client connecting back to Revit's pipe server
- **CatalogService.cs** — Tool catalog management
- **InstanceManager.cs** — Instance lifecycle
- **GatewayTunnelClient.cs** — Gateway tunnel support

### Add-in Host (`source/DevTools.Execution/External/Mcp/`)

```mermaid
flowchart LR
    subgraph Registry["Registry"]
        DotNetP["DotnetToolRegistryProvider"]
        PythonP["PythonToolRegistryProvider"]
        Catalog["ToolRegistryCatalogLoader"]
    end

    subgraph Dispatch["Dispatchers"]
        ToolD["ToolExecutionDispatcher"]
        PromptD["PromptExecutionDispatcher"]
        ResourceD["ResourceExecutionDispatcher"]
    end

    subgraph Store["Store"]
        Store["ToolRegistryStore\n(cache + change notif)"]
    end

    Registry --> Store
    Store --> Dispatch
```

---

## Key Flows

### Tool Discovery

```mermaid
sequenceDiagram
    participant Client as MCP Client
    participant Pipe as DevToolsPipeServer
    participant Handler as RegistryRequestHandler
    participant Store as ToolRegistryStore
    participant DotNet as DotnetToolRegistryProvider
    participant Python as PythonToolRegistryProvider

    Client->>Pipe: tools/list
    Pipe->>Handler: HandleToolsListAsync()
    Handler->>Store: EnsureLoaded()
    Store->>DotNet: Discover .NET tools
    Store->>Python: Discover Python tools
    DotNet-->>Store: tools
    Python-->>Store: tools
    Store-->>Handler: cached tools
    Handler-->>Pipe: JSON response
    Pipe-->>Client: tool list
```

### Tool Execution

```mermaid
sequenceDiagram
    participant Client as MCP Client
    participant Pipe as DevToolsPipeServer
    participant Handler as RegistryRequestHandler
    participant Dispatch as ToolExecutionDispatcher
    participant Context as Execution Context

    Client->>Pipe: tools/call (name + args)
    Pipe->>Handler: HandleToolsCallAsync()
    Handler->>Dispatch: Dispatch(tool, args)
    Dispatch->>Context: Execute in appropriate context
    Context-->>Dispatch: result
    Dispatch-->>Handler: result
    Handler-->>Pipe: JSON response
    Pipe-->>Client: tool result
```

---

## Related Documentation

- **[Execution Architecture](../Execution/README.md)** — Execution engine and pipe server
- **[PythonDemo Architecture](../PythonDemo/README.md)** — Python MCP toolset examples
- **Samples/McpToolsetDemo/** — Demo MCP toolset assembly
- **Samples/RevitMcpToolSet/** — Comprehensive MCP tool set

---

_Last updated: 2026-05-03_
