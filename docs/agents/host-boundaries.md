# Host Boundaries

The platform is host-agnostic by design. Every feature should be sharable across hosts by default. Only features that inherently require a specific host API (e.g. Revit DirectContext3D geometry) belong in host projects. Revit and AutoCAD are current hosts; future hosts can be added through adapters.

## Shared Layer

Keep these host-neutral — this is the default for all new functionality:

- `source/DevTools.Hosting/` — `HostApp`, `IHostAppInfo`, generic launch engine (`AddHostLaunchCore`, `HostLaunchWait`). No Revit/Acad product strings, dialog catalogs, or assembly-load policy. Stays `net48;net8.0-windows;net10.0-windows` because add-ins and NUnit.Host load identity types in-process.
- `source/DevTools.FileMetadata.Core/` — `IFileReader` / `FileInfoResult` (MCP `read_file_info`). Takes `HostApp` as a result field only. `net10.0-windows` only (Daemon / Mcp.Server / Runner).
- `source/DevTools.Execution/` — execution engine, script providers, MCP in-host runtime
- `source/DevTools.Execution.Abstractions/` — host-neutral contracts (`IHostContextExecutor`, `ICommandDiscovery`, `ICommandRunner`, `IDocumentBridge`, enums)
- `source/DevTools.Ipc/` — IPC transport (BridgeMessage, pipe connection, wire protocol)
- `source/DevTools.Mcp.Core/`, `DevTools.Mcp.Catalog/`, `DevTools.Mcp.Adapter/`, `DevTools.Mcp.Client/`, `DevTools.Mcp.Server/` — MCP platform modules
- `source/DevTools.Logging/`
- `source/DevTools.Presentation/`
- `source/DevTools.Settings/`
- `source/DevTools.Telemetry/`
- `source/DevTools.UI/`
- `source/DevTools.Utilities/` — helpers only. Leaf: no Hosting, no Execution.Abstractions, and no assembly-loading ownership.
- `source/DevTools.Testing.*` / `DevTools.NUnit.*` / `DevTools.TestAdapter` / `DevTools.TestRunner*` — host-API-free testing stack. Testhost (adapter + MTP) vs in-host (`Testing.Host` + NUnit Host/Runtime) vs Runner exe are **separate release artifacts**; see [`docs/architecture/Testing/README.md`](../architecture/Testing/README.md). `Testing.*` must not reference `NUnit.*`.

## Host Layer

Host API references belong in host projects:

- Revit host: `source/RevitDevTool/`
- Revit-only core: `source/RevitDevTool.Core/` (RevitContext, RevitTransactionService, dockable pane loader, image exporter — not shared with other hosts)
- AutoCAD host: `source/AcadDevTool/`
- Launch specs (path / argv / dialog catalog): `source/DevTools.Hosting.Revit/`, `source/DevTools.Hosting.Acad/`. `net10.0-windows` only. Daemon and TestRunner call `AddRevitLaunch` / `AddAutocadFamilyLaunch`. Add-ins do **not**.
- Offline file parse: `source/DevTools.FileMetadata.Revit/` (OpenMcdf) and `FileMetadata.Acad` (ACadSharp). `net10.0-windows` only. Parsers stay **HostApp-free**. Daemon wires `RevitFileMetadataReader.TryReadRevitVersion` into `AddRevitLaunch`; Runner passes `null`. Do not ProjectReference FileMetadata from `Hosting.Revit`.
- Add-in composition: `source/RevitDevTool/Composition/`, `source/AcadDevTool/Composition/` (`RevitServiceRegistration` / `AcadServiceRegistration`). Not `DevTools.Hosting`.
- In-process host APIs are concrete parent bindings supplied by each command isolation plan. Do not add ambient host-name lists or startup assembly-sharing policy.
- In-host MCP tools (host-bound): `source/DevTools.Mcp.Revit/`, `source/DevTools.Mcp.Acad/` (`IBuiltInMcpTool` / `IBuiltInMcpResource`). Registered from add-in `Composition/`. The `Mcp.*` prefix is not a neutrality claim.
- Visualization: `source/RevitDevTool/Visualization/` (DirectContext3D — entirely Revit-host, not in shared code)
- Future hosts: add new host projects rather than extending shared code with platform-specific branches.

## Assembly load (three jobs)

Product behavior and invariants are authoritative in
[`docs/product/assembly-isolation.md`](../product/assembly-isolation.md). This
section only maps those rules to repository owners.

`DevTools.AssemblyIsolation` owns the shared assembly-load kernel. Do not add a
fourth path. Feature adapters compose its plans: `CommandIsolationPlan`,
`ScriptIsolationPlan`, `McpToolsetIsolationPlan`, `NUnitIsolationPlan`, and the
host command-discovery metadata sessions. `Share(assembly)` reuses a loaded
copy (version may differ). `Pin(assembly)` requires full identity. Neither is
an ambient name list. PyRevit uses
its application-lifetime `AssemblyLoader` for selected extension
candidates.

| Job | Entry |
|-----|--------|
| Add-in deploy folder, once | `AssemblyIsolation/Loading/AssemblyLoader` (`LoadPath` + `Register`) |
| Dynamic / command ALC | `AssemblyIsolation` session composed by the command feature's isolation plan |
| NUnit generation | `AssemblyIsolation` session composed by NUnit's generation plan; net48 stays in the host default AppDomain (`Isolated`) |

`DevTools.TestAdapter/RuntimeAssemblyResolver` is the sole bootstrap exception:
it resolves the package's private closure from `AppContext.BaseDirectory` before
the kernel can be loaded. It registers once, accepts only full simple assembly
identities that match the candidate exactly, and must not acquire shared-prefix,
host, or feature-execution policy.

The shipped `PythonNetStubGenerator` uses the kernel's `AssemblyLoader` for
caller-selected DLL directories. It
does not probe shared framework installations or apply `System.*`/`Microsoft.*`
sharing rules; unresolved framework requests fall back to the CLR.

Native dialog/stdio P/Invoke for **launch** stays inside `DevTools.Hosting` (`DialogNative`, `HostLaunchService.StdioInheritance`). Do not create `Hosting → Utilities` for Interop. WPF owner/title-bar stays `DevTools.UI/Win32Utils`.

## Standalone Daemon

- `source/DevTools.Daemon/` runs outside hosts as `DevTools.Daemon.exe` (WPF tray app).
- `HostBroker` discovers SDK MCP pipes (`DevToolsMcp_{Host}_{Version}_{PID}`) and owns `HostCatalog`.
- Pytest/control uses `DevTools_{Host}_{Version}_{PID}` (`DevToolsPipeServer`).
- Daemon external tools: infrastructure (`list_host_instances`, `launch_host`, `read_file_info`, `list_machines`) plus `search_dynamic` / `invoke_dynamic`.
- Fixed prompts (`revit_code`, `acad_code`) are daemon-owned.
- In-host built-in tools (shared runtime): `execute_csharp_code`, `open_document` via `IDocumentBridge`.
- Startup dialog catalogs are **per host spec** (`RevitStartupDialogSpec` / `AcadStartupDialogSpec`), not a merged Autodesk bag. Generic Hosting polls EnumWindows + BM_CLICK with **no** product keywords and **no** self-timeout. MCP and NUnit share `HostLaunchWait.UntilAsync` (one wait loop, caller ready-probe). Timeout is the safety valve (`launch_host` 2 min, adapter `LaunchTimeout`). See [0018](../decisions/0018-host-identity-and-out-of-process-infrastructure.md).
- Remaining gaps for AutoCAD: no shipped MCP toolset.

## Boundary Checklist

- Default: new features go into shared `DevTools.*` libraries unless they require host-specific APIs.
- Shared services should depend on interfaces, not Revit/AutoCAD/Tekla/Bentley APIs.
- Host projects should implement adapters for command discovery, host context execution, script bridges, debugger bridges, document bridge (`IDocumentBridge`), and visualization.
- UI/view models in shared presentation code should expose host-neutral behavior.
- Host-specific rendering, transactions, threading, and document context must stay in host projects.
- When adding a feature to one host, evaluate whether the design can be shared or extracted into a shared abstraction for future hosts.
