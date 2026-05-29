# Host Boundaries

The platform is becoming host-agnostic. Revit and AutoCAD are current hosts; future hosts can be added through adapters.

## Shared Layer

Keep these host-neutral unless there is a strong reason:

- `source/DevTools.Execution/`
- `source/DevTools.Logging/`
- `source/DevTools.McpParser/`
- `source/DevTools.McpServer/`
- `source/DevTools.Presentation/`
- `source/DevTools.Settings/`
- `source/DevTools.Telemetry/`
- `source/DevTools.UI/`
- `source/DevTools.Utilities/`

## Host Layer

Host API references belong in host projects:

- Revit: `source/RevitDevTool/`
- AutoCAD: `source/AcadDevTool/`
- Future hosts: add new host projects rather than extending shared code with platform-specific branches.

## Boundary Checklist

- Shared services should depend on interfaces, not Revit/AutoCAD/Tekla/Bentley APIs.
- Host projects should implement adapters for command discovery, host context execution, script bridges, debugger bridges, and visualization.
- UI/view models in shared presentation code should expose host-neutral behavior.
- Host-specific rendering, transactions, threading, and document context must stay in host projects.
