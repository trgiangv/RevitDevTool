# Host Boundaries

The platform is host-agnostic by design. Every feature should be sharable across hosts by default. Only features that inherently require a specific host API (e.g. Revit DirectContext3D geometry) belong in host projects. Revit and AutoCAD are current hosts; future hosts can be added through adapters.

## Shared Layer

Keep these host-neutral — this is the default for all new functionality:

- `source/DevTools.Execution/` — execution engine, script providers, MCP in-host runtime
- `source/DevTools.Execution.Abstractions/` — host-neutral contracts (`IHostContextExecutor`, `ICommandDiscovery`, `ICommandRunner`, `IDocumentBridge`, enums)
- `source/DevTools.Ipc/` — IPC transport (BridgeMessage, pipe connection, wire protocol)
- `source/DevTools.Mcp.Core/`, `DevTools.Mcp.Catalog/`, `DevTools.Mcp.Adapter/`, `DevTools.Mcp.Client/`, `DevTools.Mcp.Server/` — MCP platform modules
- `source/DevTools.Logging/`
- `source/DevTools.Presentation/`
- `source/DevTools.Settings/`
- `source/DevTools.Telemetry/`
- `source/DevTools.UI/`
- `source/DevTools.Utilities/`

## Host Layer

Host API references belong in host projects:

- Revit host: `source/RevitDevTool/`
- Revit-only core: `source/RevitDevTool.Core/` (RevitContext, RevitTransactionService, dockable pane loader, image exporter — not shared with other hosts)
- AutoCAD host: `source/AcadDevTool/`
- Visualization: `source/RevitDevTool/Visualization/` (DirectContext3D — entirely Revit-host, not in shared code)
- Future hosts: add new host projects rather than extending shared code with platform-specific branches.

## Standalone Daemon

- `source/DevTools.Daemon/` runs outside hosts as `DevTools.Daemon.exe` (WPF tray app).
- `HostBroker` discovers SDK MCP pipes (`DevToolsMcp_{Host}_{Version}_{PID}`) and owns `HostCatalog`.
- Pytest/control uses `DevTools_{Host}_{Version}_{PID}` (`DevToolsPipeServer`).
- Daemon external tools: infrastructure (`list_host_instances`, `launch_host`, `read_file_info`, `list_machines`) plus `search_dynamic` / `invoke_dynamic`.
- Fixed prompts (`revit_code`, `acad_code`) are daemon-owned.
- In-host built-in tools (shared runtime): `execute_csharp_code`, `open_document` via `IDocumentBridge`.
- Startup dialog resolver uses merged keywords in default `StartupDialogResolverOptions` (Revit + AutoCAD).
- Remaining gaps for AutoCAD: no shipped MCP toolset.

## Boundary Checklist

- Default: new features go into shared `DevTools.*` libraries unless they require host-specific APIs.
- Shared services should depend on interfaces, not Revit/AutoCAD/Tekla/Bentley APIs.
- Host projects should implement adapters for command discovery, host context execution, script bridges, debugger bridges, document bridge (`IDocumentBridge`), and visualization.
- UI/view models in shared presentation code should expose host-neutral behavior.
- Host-specific rendering, transactions, threading, and document context must stay in host projects.
- When adding a feature to one host, evaluate whether the design can be shared or extracted into a shared abstraction for future hosts.
