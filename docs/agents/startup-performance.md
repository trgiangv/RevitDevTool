# Startup Performance

Startup-sensitive code includes host boot, DI registration, command discovery, Python initialization, MCP registry loading, logging setup, and UI construction.

## Key Startup Paths

| Component | Entry point | Hot path concern |
|-----------|-------------|------------------|
| Host DI registration | `RevitHostingExtensions.AddExecutionServices()` / `AcadHostingExtensions` | Registers all services; keep cheap |
| Command discovery | `ExecutionOrchestrator` (started by FileWatcherService) | Scans configured roots for scripts/assemblies |
| Python init | `PythonInitializer` via `PixiEnvironmentProvider` | First Python call resolves pixi env |
| MCP catalog | `McpCatalogStore` / `McpCatalogLoader` | Loads tool/prompt/resource metadata from assemblies |
| Pipe server | `HostMcpServerHostedService` (hosted service) | Starts the standard MCP named-pipe listener |
| File watcher | `FileWatcherService` | Monitors configured roots with debounced events |
| Script factories | `RevitScriptExecutionStrategyFactory` (Revit uses `registerDefaultScriptProvider: false`) | Defers strategy creation until first execution |

## Rules

- Prefer lazy initialization for Python, MCP catalog loading, package inspection, and expensive discovery.
- Do not block host startup on network, package restore, Python package installation, or sample scanning.
- Keep file watchers scoped to configured roots.
- Avoid reflection scans outside explicit user-configured paths or known host bundles.
- Keep startup logging useful but bounded.

## Verification

- Build the touched host/year.
- If startup behavior changed, collect logs with `scripts/collect-logs.ps1`.
- For manual profiling, use `scripts/startup-profile.ps1` to record process timing notes and relevant log locations.
- Log root: `%APPDATA%\RevitDevTool` (contains `daemon-tray.log`, `daemon-stdio.log`, host-specific logs).
