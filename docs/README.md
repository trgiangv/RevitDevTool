# RevitDevTool - Architecture Documentation

This folder contains internal architecture documentation for the RevitDevTool platform.

RevitDevTool is evolving into a reusable .NET host/dev-tool platform. Revit and AutoCAD are current hosts; shared platform behavior lives in `source/DevTools.*`.

**User guides and tutorials:** [RevitDevTool.Wiki](https://github.com/trgiangv/RevitDevTool/wiki)

---

## Directory Structure

```
docs/
├── ai/                                # Agent workflow routing and deterministic memory
│   ├── index.md                       # Task router — start here for agents
│   ├── build-matrix.md                # SDK, TFM matrix, build/pack/deploy commands
│   ├── verification.md                # Test reality, focused verification commands
│   ├── execution-system.md            # Execution engine digest + change checklist
│   ├── mcp-pytest-bridge.md           # MCP + pytest bridge digest
│   ├── host-boundaries.md             # Shared vs host layer split
│   ├── startup-performance.md         # Lazy-init rules, profiling commands
│   ├── known-test-gaps.md             # Shallow coverage, stale paths, env deps
│   └── decision-log.md               # Durable architecture decisions (ADRs)
├── Execution/
│   └── README.md                      # Execution engine architecture
├── Logging/
│   └── README.md                      # Logging system architecture
├── Visualization/
│   └── README.md                      # DirectContext3D visualization (Revit-only)
├── PythonDemo/
│   └── README.md                      # Python + WebView2 dashboard samples
├── MCP/
│   └── README.md                      # MCP parser & server design
├── PyTest/
│   └── README.md                      # pytest bridge architecture
└── static/icons/                      # Inkscape SVG sources for UI/installer icons
    ├── Commands-*.svg                 # Commands panel icons
    ├── DevTools-*.svg                 # Main DevTools tab icons
    ├── StubBuilder-*.svg              # Stub builder icons
    └── installer/                     # Installer banner/background
```

---

## Module Documentation

### AI Harness
**Path:** `ai/index.md`

Operational workflow for coding agents:
- Task routing to module docs and skill checklists
- Build/test/package command selection
- Known harness gaps and host-boundary rules
- Verification loop guidance

This is an agent digest. It does not replace the architecture docs below.

### Execution
**Path:** `Execution/README.md`

Comprehensive architecture documentation covering:
- Host-adapter execution model for Revit, AutoCAD, and future .NET hosts
- Execution modes: .NET assemblies, Python, IronPython, F#, C# scripts
- `IExecutionProvider` / `IExecutionStrategy` interfaces
- Tree node model (Root → Intermediate → Executable)
- `ExecutionOrchestrator` event-driven lifecycle
- PEP 723 dependency resolution (Pixi + Pip backends)
- F# NuGet and C# Roslyn script compilation
- `FileWatcherService` 3-layer debounced watching
- `TreeStateManager` state persistence
- MCP bridge & Named Pipe server integration
- Package management (`PackageService`)

### Logging
**Path:** `Logging/README.md`

Unified logging infrastructure:
- Multi-sink output (monitor, file, HTTP)
- Keyword-based log level detection
- Host-specific context enrichment
- Revit geometry interception → visualization routing
- Console + Python `print()` redirection
- Revit element linkification

### Visualization
**Path:** `Visualization/README.md`

DirectContext3D transient geometry rendering:
- Revit-host feature, not shared platform rendering
- Server-per-geometry-type pattern
- Two-pass rendering (opaque + transparent)
- RenderingBufferStorage caching
- Integration with logging via `GeometryListener`

### PythonDemo
**Path:** `PythonDemo/README.md`

Python + WebView2 dashboard application:
- 17 demo scripts (data analysis, visualization, logging, ML, geometry)
- React + TypeScript frontend with Recharts
- Polars analytics engine
- WebView2 embedded browser integration
- MCP toolset examples

### MCP
**Path:** `MCP/README.md`

Model Context Protocol integration:
- `DevTools.McpParser` — message parsing library
- `DevTools.Daemon` — standalone tray app (MCP engine, auth, gateway)
- `ToolRegistryStore` — in-process tool discovery & caching
- `.NET` + Python toolset providers
- Tool/Prompt/Resource dispatch
- Standalone helper tools are multi-host (`launch_host` supports Revit and AutoCAD, `read_file_info` reads RVT/RFA/DWG metadata, `open_model` detects host from extension); in-host MCP runtime is shared

### PyTest
**Path:** `PyTest/README.md`

pytest remote execution bridge:
- Named Pipe protocol
- Multi-host `revitdevtool_pytest` client plugin (Revit, AutoCAD-family, extensible)
- `PytestExecutionService` + `PytestRunner.py` server
- Test progress streaming & result reporting
- Current tests are smoke/contract level, not deep end-to-end assurance

---

## Quick Navigation

| I want to... | Read |
|-------------|------|
| Route an agent task | [ai/index.md](ai/index.md) |
| Understand build/deploy | [ai/build-matrix.md](ai/build-matrix.md) |
| Understand the execution engine | [Execution/README.md](Execution/README.md) |
| Understand logging | [Logging/README.md](Logging/README.md) |
| Understand visualization (Revit-only) | [Visualization/README.md](Visualization/README.md) |
| Understand MCP integration | [MCP/README.md](MCP/README.md) |
| Understand pytest bridge | [PyTest/README.md](PyTest/README.md) |
| Understand host boundaries | [ai/host-boundaries.md](ai/host-boundaries.md) |
| Understand known test gaps | [ai/known-test-gaps.md](ai/known-test-gaps.md) |
| Use RevitDevTool (end-user) | [RevitDevTool.Wiki](https://github.com/trgiangv/RevitDevTool/wiki) |

---

## Documentation Completeness

### Architecture Modules

| Module | Architecture Docs | Status |
|--------|-------------------|--------|
| **Execution** | ✅ `Execution/README.md` | Production |
| **Logging** | ✅ `Logging/README.md` | Production |
| **Visualization** | ✅ `Visualization/README.md` | Production (Revit-only) |
| **MCP** | ✅ `MCP/README.md` | Production |
| **PyTest** | ✅ `PyTest/README.md` | Production |
| **PythonDemo** | ✅ `PythonDemo/README.md` | Samples |
| **AI Harness** | ✅ `ai/index.md` + 8 digests | Agent workflow |

### Shared Platform Libraries (no dedicated README — covered by module docs and AGENTS.md)

| Library | Covered in | Notes |
|---------|-----------|-------|
| **DevTools.Presentation** | `Execution/README.md`, `Logging/README.md` | MVVM shell, ViewModels, host-neutral UI |
| **DevTools.UI** | `AGENTS.md` | WPF controls, theme, MahApps integration |
| **DevTools.Settings** | `Execution/README.md`, `MCP/README.md` | Configuration persistence |
| **DevTools.Telemetry** | `AGENTS.md` | Sentry integration, path scrubbing |
| **DevTools.Utilities** | `AGENTS.md` | Win32 helpers, assembly loading |
| **DevTools.McpParser** | `MCP/README.md` | Shared bridge contracts and parsers |
| **DevTools.Daemon** | `MCP/daemon.md`, `ai/build-matrix.md` | Standalone tray app (MCP engine, auth, gateway) |
| **RevitDevTool.Core** | `AGENTS.md` | Revit-only: transactions, dockable panes |

### Sample Projects (source only, no architecture docs)

| Sample | Type |
|--------|------|
| `Samples/CSharpDemo/` | Revit C# demo |
| `Samples/AcadCSharpDemo/` | AutoCAD C# demo |
| `Samples/FSharpDemo/` | Revit F# demo |
| `Samples/CSharpScriptDemo/` | C# `.csx` script demo |
| `Samples/McpToolsetDemo/` | MCP `[McpTool]` attribute demo |
| `Samples/RevitMcpToolSet/` | Revit MCP toolset sample |
| `Samples/PythonDemo/` | Python + React dashboard (not in `.slnx`) |

---

## Related Links

### Source Code

| Project | Path | Layer |
|---------|------|-------|
| Execution engine | `source/DevTools.Execution/` | Shared |
| Presentation (MVVM) | `source/DevTools.Presentation/` | Shared |
| Logging | `source/DevTools.Logging/` | Shared |
| MCP parser/contracts | `source/DevTools.McpParser/` | Shared |
| MCP daemon (tray app) | `source/DevTools.Daemon/` | Standalone |
| Settings | `source/DevTools.Settings/` | Shared |
| Telemetry | `source/DevTools.Telemetry/` | Shared |
| UI (WPF controls) | `source/DevTools.UI/` | Shared |
| Utilities | `source/DevTools.Utilities/` | Shared |
| Revit host | `source/RevitDevTool/` | Host |
| Revit core helpers | `source/RevitDevTool.Core/` | Host (Revit-only) |
| AutoCAD host | `source/AcadDevTool/` | Host |

### Samples

- [C# demo](../Samples/CSharpDemo/) | [C# scripts](../Samples/CSharpScriptDemo/) | [F# demo](../Samples/FSharpDemo/)
- [Python demo](../Samples/PythonDemo/commands/) | [MCP toolset demo](../Samples/McpToolsetDemo/)
- [Revit MCP toolset](../Samples/RevitMcpToolSet/) | [AutoCAD demo](../Samples/AcadCSharpDemo/)

---

## Contributing

1. **Read architecture docs** for the module you're modifying
2. **Follow existing patterns** (Provider, Strategy, Composite, Observer)
3. **Update docs** when changing important architecture or feature boundaries
4. **Add demos** in `Samples/`
5. **Update wiki** for user-facing changes

---

## Design Philosophy

1. **Sharable by Default** — Every feature should be host-agnostic unless it inherently requires a specific host API. Only Revit-exclusive features (e.g. DirectContext3D) belong in host projects.
2. **Separation of Concerns** — Each module has clear responsibilities; shared platform vs host adapters.
3. **Extensibility** — Provider/Strategy patterns for pluggable behavior across hosts.
4. **Performance** — Buffering, caching, async throughout.
5. **Type Safety** — Strong typing with nullability annotations.
6. **Testability** — Dependency injection and mockable interfaces.

---

_Last updated: 2026-05-31_
