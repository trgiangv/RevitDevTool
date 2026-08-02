# Architecture

Thin index of structural truth. Module detail lives under `docs/architecture/`.
Agent operating digests live under `docs/agents/`. Product behavior contracts live
under `docs/product/`.

## Dependency Direction

```text
Host projects (RevitDevTool, AcadDevTool)
  -> shared platform (source/DevTools.*)
  -> host-only helpers only when API-bound (e.g. RevitDevTool.Core)
```

Default: features are sharable. Host API, threading, and host-specific rendering
stay in host projects. Visualization (DirectContext3D) is Revit-host only.

## Module Map

| Module | Architecture | Product contract | Agent digest |
|--------|--------------|------------------|--------------|
| Execution | [architecture/Execution](architecture/Execution/README.md) | [product/execution.md](product/execution.md) | [agents/execution-system.md](agents/execution-system.md) |
| MCP | [architecture/MCP](architecture/MCP/README.md) | [product/mcp.md](product/mcp.md) | [agents/mcp-pytest-bridge.md](agents/mcp-pytest-bridge.md) |
| PyTest bridge | [architecture/PyTest](architecture/PyTest/README.md) | [product/pytest-bridge.md](product/pytest-bridge.md) | [agents/mcp-pytest-bridge.md](agents/mcp-pytest-bridge.md) |
| Logging | [architecture/Logging](architecture/Logging/README.md) | [product/logging.md](product/logging.md) | — |
| Visualization | [architecture/Visualization](architecture/Visualization/README.md) | [product/visualization.md](product/visualization.md) | — |
| PythonDemo | [architecture/PythonDemo](architecture/PythonDemo/README.md) | — | — |

## Cross-Cutting

| Topic | Read |
|-------|------|
| Host boundaries | [agents/host-boundaries.md](agents/host-boundaries.md) |
| Build / TFM matrix | [agents/build-matrix.md](agents/build-matrix.md) |
| Verification | [agents/verification.md](agents/verification.md) |
| Known test gaps | [agents/known-test-gaps.md](agents/known-test-gaps.md) |
| Lasting decisions | [decisions/](decisions/README.md) |

## Source Layout

- Shared: `source/DevTools.*` (Execution, Execution.Abstractions, Ipc, Mcp.Core/Catalog/Adapter/Client/Server, FileMetadata.Core/Revit/Acad, Logging, Presentation, Settings, Telemetry, UI, Utilities, Daemon)
- Revit host: `source/RevitDevTool/`; Revit-only helpers: `source/RevitDevTool.Core/`
- AutoCAD host: `source/AcadDevTool/`
- Samples: `samples/`; build: `build/`; scripts: `scripts/`
