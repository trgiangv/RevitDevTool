# RevitDevTool — Architecture Documentation

This folder contains **internal architecture documentation** for developers contributing to RevitDevTool.

**User guides and tutorials:** [RevitDevTool.Wiki](https://github.com/trgiangv/RevitDevTool/wiki)

---

## Directory Structure

```
docs/
├── Execution/
│   └── README.md           # Execution engine architecture
├── Logging/
│   └── README.md           # Logging system architecture
├── Visualization/
│   └── README.md           # DirectContext3D visualization
├── PythonDemo/
│   └── README.md           # Python + WebView2 dashboard
├── MCP/
│   └── README.md           # MCP parser & server design
└── PyTest/
    └── README.md           # pytest bridge architecture
```

---

## Module Documentation

### Execution
**Path:** `Execution/README.md`

Comprehensive architecture documentation covering:
- 3 execution modes: .NET DLL, Python script, F# script
- `IExecutionProvider` / `IExecutionStrategy` interfaces
- Tree node model (Root → Intermediate → Executable)
- `ExecutionOrchestrator` event-driven lifecycle
- PEP 723 dependency resolution (Pixi + Pip backends)
- F# NuGet dependency resolution
- `FileWatcherService` 3-layer debounced watching
- `TreeStateManager` state persistence
- MCP bridge & Named Pipe server integration
- Package management (`PackageService`)

### Logging
**Path:** `Logging/README.md`

Unified logging infrastructure:
- Multi-sink output (monitor, file, HTTP)
- Keyword-based log level detection
- Geometry interception → visualization routing
- Console + Python `print()` redirection
- Revit element linkification

### Visualization
**Path:** `Visualization/README.md`

DirectContext3D transient geometry rendering:
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
- `DevTools.McpServer` — standalone server binary
- `ToolRegistryStore` — in-process tool discovery & caching
- `.NET` + Python toolset providers
- Tool/Prompt/Resource dispatch

### PyTest
**Path:** `PyTest/README.md`

pytest remote execution bridge:
- Named Pipe protocol
- `revitdevtool_pytest` client plugin
- `PytestExecutionService` + `PytestRunner.py` server
- Test progress streaming & result reporting

---

## Quick Navigation

| I want to... | Read |
|-------------|------|
| Understand the execution engine | [Execution/README.md](Execution/README.md) |
| Understand logging | [Logging/README.md](Logging/README.md) |
| Understand visualization | [Visualization/README.md](Visualization/README.md) |
| Understand MCP integration | [MCP/README.md](MCP/README.md) |
| Understand pytest bridge | [PyTest/README.md](PyTest/README.md) |
| Use RevitDevTool | [RevitDevTool.Wiki](https://github.com/trgiangv/RevitDevTool/wiki) |

---

## Documentation Completeness

| Module | Architecture Docs | Status |
|--------|-------------------|--------|
| **Execution** | ✅ `README.md` | Production |
| **Logging** | ✅ `README.md` | Production |
| **Visualization** | ✅ `README.md` | Production |
| **PythonDemo** | ✅ `README.md` | Production |
| **MCP** | ✅ `README.md` | Integration |
| **PyTest** | ✅ `README.md` | Integration |
| **CSharpDemo** | ⚠️ Source only | Examples |
| **FSharpDemo** | ⚠️ Source only | Examples |
| **FSharpScriptDemo** | ⚠️ Source only | Examples |
| **McpToolsetDemo** | ⚠️ Source only | Examples |
| **RevitMcpToolSet** | ⚠️ Source only | Examples |

---

## Related Links

- **Source Code:** [../source/](../source/)
- **Main engine:** `source/DevTools.Execution/`
- **Logging library:** `source/DevTools.Logging/`
- **MCP parser:** `source/DevTools.McpParser/`
- **MCP server:** `source/DevTools.McpServer/`
- **C# samples:** [../Samples/CSharpDemo/](../Samples/CSharpDemo/)
- **F# samples:** [../Samples/FSharpDemo/](../Samples/FSharpDemo/)
- **Python samples:** [../Samples/PythonDemo/commands/](../Samples/PythonDemo/commands/)

---

## Contributing

1. **Read architecture docs** for the module you're modifying
2. **Follow existing patterns** (Provider, Strategy, Composite, Observer)
3. **Update docs** if you change internal design
4. **Add demos** in `Samples/`
5. **Update wiki** for user-facing changes

---

## Design Philosophy

1. **Separation of Concerns** — Each module has clear responsibilities
2. **Extensibility** — Provider/Strategy patterns for pluggable behavior
3. **Performance** — Buffering, caching, async throughout
4. **Type Safety** — Strong typing with nullability annotations
5. **Testability** — Dependency injection and mockable interfaces

---

_Last updated: 2026-05-03_
