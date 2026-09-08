# Assembly isolation

RevitDevTool loads add-ins, commands, scripts, MCP toolsets, and NUnit runtimes
without allowing one feature's dependency policy to leak into another.
`DevTools.AssemblyIsolation` is the single shared kernel for this behavior.

## Contract

- Sharing is explicit and uses a concrete `Assembly` instance.
- `Share(assembly)` reuses that exact loaded instance. Name, culture, and
  public-key token must match. Version is forgiven for that assembly only and
  is silent. Host Autodesk APIs use compile-time `typeof` anchors, and may
  also Share identities already in the default context
  (`AssemblyHelper.Find` / `FindMany` — never loads from disk). Host adapters
  subclass `HostAssemblies` and only supply `LoadedByType` / `LoadedByName`. Nothing is
  shared by prefix.
- `Pin(assembly)` reuses a loaded copy only when the full identity matches.
  Feature contracts (`nunit.framework`, MCP protocol types,
  `ITestingRuntimeSession`) stay pinned.
- Private managed candidates still require full identity. On net48 a
  `ManifestAssemblySource` is a **redirect closure** (the testhost
  `app.config` role, scoped to one Isolated session): a request may bind a
  **newer** file already in that manifest, or a copy this session already
  loaded (`NetfxClosureBind` — name, culture, token; never a downgrade).
  Nested `AssemblyResolve` while `LoadFile` is still running is served by this
  session (`activeLoads`) so a payload Bcl 10 does not bind Carbon Insights'
  Tasks.Extensions. The session never selects a DefaultDomain assembly it did
  not load (Speckle, Carbon Insights, pyRevit). Directory sources (commands,
  MCP toolsets) and CoreCLR stay exact. Workload assemblies still `LoadFile`
  under `WithDistinctFileIdentity` so two generation shadows of the same Engine
  identity stay distinct. Compile-ref vs nupkg assembly version (for example
  Tasks.Extensions 4.2.1.0 vs 4.2.4.0) is this closure rule, not a TUnit
  name list.
  Directory traversal and reparse-point escapes are rejected, including add-in
  directory resolution.
- `System.*`, `Microsoft.*`, Autodesk APIs, and UI libraries are not shared by
  prefix. DevTools forks (`DevTools.MahApps.Metro`, `DevTools.ControlzEx`,
  `DevTools.Microsoft.Xaml.Behaviors`) are separate identities and stay private
  to the host add-in. WPF resource/theme assemblies must not load twice in one
  process — a second copy breaks styling.
- Unresolved framework dependencies fall back to the CLR after private sources
  have declined the request.
- Metadata discovery never executes the inspected assembly. When the host
  install and a command folder both ship the same identity (Revit 2025
  `Microsoft.Xaml.Behaviors` plus a copied NuGet), metadata keeps the first
  path and continues; it does not fail parse.
- Feature plans set `Isolated`. The kernel maps that to a collectible ALC on
  modern TFMs and a scoped `AssemblyResolve` hook on net48. That hook is
  **prepended**: net48 returns the first non-null handler, so `Pin`/`Share`
  still run before earlier simple-name resolvers (Costura). Returning null
  leaves later handlers free to serve their own assemblies. `Collectible`
  remains explicit when a test must name the ALC (and throws on net48).

## Kinds

| Kind | Product behavior |
|----------|------------------|
| Permanent | Add-in-shipped assemblies are path-loaded once for the process lifetime so WPF resources and dependency locations remain stable. They are not hot-reloaded. Implemented by `AssemblyLoader`, not `AssemblyIsolationSession`. |
| Isolated | Feature default. Kernel maps to collectible (modern TFM) or scoped net48. Scripts, MCP toolsets, commands, and NUnit/TUnit generations use this. Command sessions stay alive after `Execute` so modeless host UI is not torn down. |
| Collectible | Explicit collectible ALC. Same unload rules as Isolated on modern TFMs. Throws on net48. |
| NUnit net48 | Same default AppDomain as the host. Generation **shadow** copies are `LoadFile`'d so the same identity can exist per generation (hot reload) without locking the project output. Host CAD APIs already loaded by Revit/AutoCAD are reused in place. Those DLLs cannot be unloaded from the process. |

Feature code continues to own compilation, discovery semantics, registries,
generation snapshots, invocation, result mapping, and logging. It composes an
isolation plan and translates structured diagnostics; the kernel contains no
Execution, MCP, NUnit, Revit, AutoCAD, or logging policy. WPF Share is identity
policy on the plan (one copy so styling stays intact), not a UI framework
dependency.

## Packaging boundary

Autodesk API references are compile-only and are not copied as runtime payloads.
Host packages merge or ship one kernel identity according to their existing
ILRepack policy. `RevitDevTool.TestAdapter` ILRepacks Ipc and Transport into
the adapter on every TFM and keeps `DevTools.Testing.Abstractions.dll` loose
so testhost MTP shares `HostTestDiscovery`.

`DevTools.TestAdapter/RuntimeAssemblyResolver` is the sole direct-loader
exception. The public platform hook must bootstrap its private runtime closure before
the shared kernel can be loaded. The resolver registers once, probes only the
application base directory, and accepts only an exact full identity.

## Proof

Architecture tests prevent new direct loaders outside the kernel and the MTP
bootstrap exception. Focused suites cover identity drift, private
`System.*`/`Microsoft.*` dependencies, managed/native containment, metadata-only
inspection, net48 hook cleanup, collectible unload, host package ownership, and
clean MTP consumers.

See [decision 0023](../decisions/0023-shared-assembly-isolation-kernel.md) for
the rationale and the [completed plan](../plans/completed/2026-08-18-assembly-isolation-kernel.md)
for implementation scope and verification.
