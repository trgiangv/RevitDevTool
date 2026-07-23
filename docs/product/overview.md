# Platform Overview

RevitDevTool is a reusable .NET host/dev-tool platform for CAD/BIM applications.
It is not a Revit-only add-in. Revit and AutoCAD are current hosts; future hosts
may include Tekla, Bentley, or other .NET-capable platforms.

## Contract

- Solution source of truth: `RevitDevTool.slnx` (no root `.sln`).
- Shared platform behavior lives in `source/DevTools.*`.
- Host API dependencies stay in host projects (`source/RevitDevTool/`,
  `source/AcadDevTool/`).
- `RevitDevTool.Core` is Revit-only (transactions, dockable panes, image export).
- Visualization (DirectContext3D) is Revit-host only under
  `source/RevitDevTool/Visualization/`.
- External MCP entry point is `DevTools.Daemon` (`--stdio` or gateway). There is
  no separate `MCPServer.exe`.

## Required Tooling

- .NET SDK `10.0.0` via `global.json`.
- Autodesk configs `Debug|Release.Autodesk.2022`–`2027` (2022–2024 → net48;
  2025–2026 → net8.0-windows; 2027 → net10.0-windows).
- Client pytest always via `uv run pytest` in sibling `RevitDevTool.PyTest`.

## Related

- Architecture index: [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md)
- Agent verify commands: [`docs/agents/verification.md`](../agents/verification.md)
- Build matrix: [`docs/agents/build-matrix.md`](../agents/build-matrix.md)
