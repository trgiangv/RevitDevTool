# Logging System Architecture

Unified logging infrastructure built on .NET `System.Diagnostics.Trace` with multi-sink output, keyword detection, and geometry interception.

**Source:** `source/DevTools.Logging/` (shared library) + `source/RevitDevTool/Logging/` (add-in host)

---

## Architecture Flow

```mermaid
flowchart TB
    subgraph Sources["Log Sources"]
        CSharp["C#: Trace.Information()"]
        Python["Python: print()"]
        Console["Console.WriteLine()"]
        Debug["Debug.WriteLine()"]
    end

    subgraph Listeners["Trace Listeners"]
        Logger["LoggerTraceListener\n(format + route)"]
        ConsoleR["ConsoleRedirector\n(intercept Console)"]
        Geometry["GeometryListener\n(detect geometry)"]
        Notify["NotifyListener\n(UI events)"]
    end

    subgraph LoggingLib["DevTools.Logging"]
        Detector["LogLevelDetector\n(keyword scan)"]
        Monitor["MonitorLogTarget\n(RichTextBox)"]
        FileLog["FileLogProcessor\n(.log / .json)"]
        HttpLog["HttpLogProcessor\n(remote)"]
    end

    subgraph RevitHost["RevitDevTool/Logging"]
        LogSvc["LoggingService\n(orchestrator)"]
        Linkify["RevitLinkifier\n(element links)"]
    end

    subgraph Viz["Visualization"]
        Router["Type Router"]
        DC3D["DirectContext3D"]
    end

    CSharp --> Logger
    Python --> Logger
    Console --> ConsoleR
    ConsoleR --> Logger
    Debug --> Logger
    Logger --> Detector
    Detector --> Monitor
    Detector --> FileLog
    Detector --> HttpLog
    Geometry --> Router
    Router --> DC3D
    Logger --> Notify
    LogSvc -.-> Logger
    LogSvc -.-> Geometry
    Linkify -.-> Monitor
```

---

## Core Components

| Component | Location | Role |
|-----------|----------|------|
| `LoggingService` | `RevitDevTool/Logging/` | Lifecycle orchestrator — init, restart, register listeners |
| `LoggerTraceListener` | `DevTools.Logging/Listeners/` | Captures all `.NET Trace` events, routes to sinks |
| `ConsoleRedirector` | `DevTools.Logging/Listeners/` | Intercepts `Console.WriteLine()` + Python `print()` |
| `GeometryListener` | `RevitDevTool/Logging/Listeners/` | Detects Revit geometry types in trace calls → routes to Visualization |
| `NotifyListener` | `DevTools.Logging/Listeners/` | Broadcasts UI update events |
| `LogLevelDetector` | `DevTools.Logging/` | Keyword-based severity detection |

---

## Log Level Detection

Keywords in message content determine severity level automatically:

```mermaid
flowchart LR
    Message["Trace.Write(msg)"] --> Scan["LogLevelDetector\nscans message text"]
    Scan --> Tier1{"CRITICAL / FATAL?"}
    Tier1 -->|yes| Critical["LogLevel.Critical"]
    Tier1 -->|no| Tier2{"ERROR / FAILED?"}
    Tier2 -->|yes| Error["LogLevel.Error"]
    Tier2 -->|no| Tier3{"WARNING / DEPRECATED?"}
    Tier3 -->|yes| Warning["LogLevel.Warning"]
    Tier3 -->|no| Info["LogLevel.Information"]
```

| Category | Keywords | Level |
|----------|----------|-------|
| **Critical** | CRITICAL, FATAL, PANIC, SECURITY, UNAUTHORIZED | `Critical` |
| **Error** | ERROR, FAILED, EXCEPTION, TIMEOUT, INVALID, NOT FOUND | `Error` |
| **Warning** | WARNING, DEPRECATED, OBSOLETE, MEMORY, LEAK, RETRY | `Warning` |
| Default | Everything else | `Information` |

---

## Geometry Interception

```mermaid
sequenceDiagram
    participant Script as User Script
    participant Trace as Trace.Write()
    participant GL as GeometryListener
    participant Router as Type Router
    participant Server as VisualizationServer
    participant DC3D as DirectContext3D

    Script->>Trace: Trace.Write(curve)
    Trace->>GL: Captures event
    GL->>GL: Is geometry type? → yes
    GL->>Router: Route to visualization
    Router->>Server: Select server by type
    Server->>DC3D: Tessellate + render
    Note over GL: If not geometry → normal text log
```

Supported geometry types: `Curve`, `Face`, `Solid`, `Mesh`, `XYZ`, `BoundingBoxXYZ`. Each is routed to its dedicated `VisualizationServer<T>`.

---

## Output Targets

| Target | Implementation | Format | Use Case |
|--------|---------------|--------|----------|
| **Monitor** | `MonitorLogTarget` | Color-coded RichTextBox | Real-time UI display |
| **File (.log)** | `FileLogProcessor` | Plain text | Persistent logging |
| **File (.json)** | `FileLogProcessor` | Structured JSON | Machine-readable export |
| **HTTP** | `HttpLogProcessor` | JSON over HTTP | Remote logging (future) |

---

## Service Composition

```mermaid
flowchart TB
    subgraph Config["Host.ConfigureLogging()"]
        direction LR
        MonLog["MonitorLoggingOptions"]
        FileLog["FileLoggingOptions"]
        HttpLog["HttpLoggingOptions"]
        Sink["LogSink selection"]
    end

    subgraph Init["LoggingService.Initialize()"]
        Register["RegisterTraceListeners()"]
        Level["SetMinimumLevel()"]
    end

    subgraph Runtime["Runtime"]
        Capture["All Trace/Console/Debug\ncaptured by listeners"]
        Process["LogLevelDetector\n+ Enrichment"]
        Output["Route to configured sinks"]
    end

    Config --> Init
    Init --> Runtime
```

DI registration via `Host.ConfigureLogging()` in `source/DevitDevTool/Host.cs`.

---

## Linkify — Revit Element References

`RevitLinkifier` (`source/RevitDevTool/Logging/Linkify/`) auto-detects Revit element IDs in log output and creates clickable links that select the element in Revit.

Pattern: `<ElementId (12345)>` in log messages is automatically converted to a navigable link.

---

## Related Modules

- **[Execution Architecture](../Execution/README.md)** — Script execution triggers logging
- **[Visualization Architecture](../Visualization/README.md)** — Geometry routing target

---

_Last updated: 2026-05-03_
