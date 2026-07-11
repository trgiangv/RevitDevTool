# Decision Log

Use this file for durable architecture decisions that affect agent behavior. Keep entries short.

## 2026-05-29: Repo-owned AI harness

- `AGENTS.md` is the entry contract and router.
- `docs/agents/` contains deterministic agent digests.
- `.agents/skills/*/SKILL.md` contains task-specific checklists.
- Tool-specific files should be thin adapters that point back to the repo-owned harness.

## 2026-05-29: Host-agnostic direction

- The project is no longer treated as Revit-only.
- Revit and AutoCAD are current hosts.
- Shared `DevTools.*` libraries should remain host-neutral unless a host API dependency is unavoidable.

## 2026-05-29: Architecture docs as source of truth

- Important features and architecture changes should update the matching docs.
- Module READMEs hold durable architecture.
- `docs/agents/` holds agent workflow and decision context.
- Skills hold short task checklists.

## 2026-05-29: GitNexus unavailable for current index run

- `npx gitnexus analyze` fails in `scopeResolution` even after ignoring vendor `libs/` and cleaning `.gitnexus`.
- `.gitnexusignore` excludes vendor/generated/runtime folders so future indexing should focus on repo-owned code.
- Until analyzer failure is resolved, agents should inspect source directly and not rely on GitNexus graph freshness.

## 2026-05-31: MCP multi-host readiness confirmed

- `MCPServer.exe` is now host-agnostic at the protocol/runtime layer: `InstanceManager` discovers any host pipe, `HostBridgeClient` (formerly `RevitBridgeClient`) connects generically.
- Standalone built-in tools: `list_host_instances`, `launch_host`, `read_file_info`, `open_model` (multi-host).
- In-host built-in tools: `execute_csharp_code`, `open_document` (registered in `ExecutionExtensions.cs`).
- In-host MCP dispatch runtime (`DevTools.Execution`) is fully shared — both `RevitDevTool` and `AcadDevTool` register the pipe server.
- Startup dialog resolver and `open_document` are implemented for AutoCAD (merged keywords in default `StartupDialogResolverOptions`; `AcadDocumentBridge` + `OpenDocumentTool`).
- Remaining AutoCAD gaps: no shipped MCP toolset. (pytest bridge client is now multi-host — scans all `DevTools_{Host}_{Version}_{PID}` pipes.)
- Design principle: every new MCP feature should be sharable by default.

## 2026-05-31: Architecture docs audit and corrections

- `RevitDevTool.Core` reclassified: it is Revit-only (transactions, dockable panes, image export), not a shared platform library. Only `RevitDevTool` references it; `AcadDevTool` does not.
- Visualization confirmed as Revit-host only: lives entirely in `source/RevitDevTool/Visualization/`, not in shared code.
- `DevTools.McpServer` standalone process clarified: runs outside hosts as `MCPServer.exe`; standalone built-in tools are multi-host, in-host MCP runtime is shared.
- `docs/README.md` directory tree expanded to show all 8 `docs/agents/` digest files and `static/icons/` assets.
- Documentation completeness table restructured: separated architecture modules, shared platform libraries, and sample projects.
- All 6 `scripts/agent/*.ps1` scripts received PowerShell comment-based help (`.SYNOPSIS`, `.DESCRIPTION`, `.PARAMETER`, `.EXAMPLE`).
- `AGENTS.md` Verification section now lists all 6 agent scripts.
- `docs/agents/host-boundaries.md` updated with `RevitDevTool.Core`, visualization location, and standalone MCP server process.

## 2026-05-31: Document bridge and startup dialog resolver

- Added shared `IDocumentBridge` with `RevitDocumentBridge` and `AcadDocumentBridge`; in-host `OpenDocumentTool` (`open_document`) delegates to host implementations.
- `StartupDialogResolver` simplified: merged Revit + AutoCAD keywords in default `StartupDialogResolverOptions`; removed `ForHost` host-specific option branching.

## 2026-05-31: Multi-host pytest client refactor

- `revitdevtool_pytest` v0.3.0 replaces all `--revit-*` CLI flags and `revit_*` INI options with `--host-*` / `host_*` equivalents.
- Pipe pattern aligned with C# `InstanceManager`: `^DevTools_\w+_[^_]+_\d+$` — vendor-prefixed to prevent false positives. Version is `[^_]+` (any non-underscore string), not `\d{4}`. Supports year (2025), semver (8.0), dotted (2024.1), or prefixed (v3.2.1).
- `HostInstance.version` changed from `int` to `str` throughout the Python client.
- `HOST_REGISTRY` expanded beyond Autodesk: added Navisworks, Rhino, Tekla entries. Hosts without `exe_name` connect via pipe auto-discovery or explicit `--host-pipe` only.
- `get_host_config()` returns a fallback `HostConfig(pipe_prefix=host_name)` for unknown hosts — any host exposing a DevToolsPipeServer pipe works without pre-registration.
- `find_host_pipes()` no longer filters out unregistered pipe prefixes — returns all pipes matching the 3-part pattern, resolving host name from registry or using the raw prefix.
- `HostConfig.exe_name` changed from required to `str | None` — hosts without exe discovery logic still work for connect-only scenarios.

## 2026-06-18: DevTools.McpServer removed — Daemon is sole MCP host

- `source/DevTools.McpServer/` deleted entirely. `DevTools.Daemon` is now the single MCP entry point for external AI clients.
- `DevTools.Daemon.exe` is a standalone WPF tray app (not a console exe). It owns: auth (OIDC/PKCE), MCP engine (stdio + gateway), host discovery, multi-machine gateway, control pipe, dashboard UI.
- Installer (`install/Setup.iss`) now installs `DevTools.Daemon.exe` to `{app}\Contents\`, registers auto-start (`HKCU\...\Run\DevToolsDaemon`), launches post-install, and unregisters on uninstall.
- Build pipeline: `PublishMcpServerModule` removed; `PublishDaemonModule` is the sole publish step. `CreateBundleModule` packs only `DevTools.Daemon.exe`.
- AI clients (Cursor, Claude Desktop) point their MCP config to `DevTools.Daemon.exe --stdio`. The `--stdio` flag runs a direct MCP server on stdin/stdout (self-contained process, no proxy). The single-instance mutex only applies to tray mode — duplicate tray launches exit silently, but stdio processes are independent.
- No backward compatibility period — MCPServer.exe is gone from the codebase and installer.
