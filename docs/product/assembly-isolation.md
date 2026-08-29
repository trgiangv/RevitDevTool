# Assembly isolation

RevitDevTool loads add-ins, commands, scripts, MCP toolsets, and NUnit runtimes
without allowing one feature's dependency policy to leak into another.
`DevTools.AssemblyIsolation` is the single shared kernel for this behavior.

## Contract

- Sharing is explicit and uses a concrete `Assembly` instance.
- `Share(assembly)` reuses a loaded copy. Version may differ; name, culture, and
  public-key token must match. Host Autodesk APIs and official WPF sidecars
  (`MahApps.Metro`, `ControlzEx`, `Microsoft.Xaml.Behaviors`) use Share so
  Nice3point compile refs resolve to the host-loaded assembly. Version drift
  emits `share-version-drift`.
- `Pin(assembly)` reuses a loaded copy only when the full identity matches.
  Feature contracts (`nunit.framework`, MCP protocol types,
  `ITestingRuntimeSession`) stay pinned.
- Private managed candidates still require full identity, except net48
  isolated resolve may bind a **newer** copy of `System.Text.Json`,
  `Microsoft.Bcl.AsyncInterfaces`, `System.IO.Pipelines`, or
  `System.Text.Encodings.Web` (`NetfxBclBind`). CoreCLR stays exact.
  Directory traversal and reparse-point escapes are rejected, including add-in
  directory resolution.
- `System.*`, `Microsoft.*`, Autodesk APIs, and UI libraries are not shared by
  prefix. DevTools forks (`DevTools.MahApps.Metro`, `DevTools.ControlzEx`,
  `DevTools.Microsoft.Xaml.Behaviors`) are separate identities and stay private
  to the host add-in. Official WPF Share is a known isolation gap—one
  process-wide identity, even when a workload ships another version—accepted
  because these libraries are mature and rarely change.
- Unresolved framework dependencies fall back to the CLR after private sources
  have declined the request.
- Metadata discovery never executes the inspected assembly. When the host
  install and a command folder both ship the same identity (Revit 2025
  `Microsoft.Xaml.Behaviors` plus a copied NuGet), metadata keeps the first
  path and continues; it does not fail parse.
- Feature plans set `Isolated`. The kernel maps that to a collectible ALC on
  modern TFMs and a scoped `AssemblyResolve` hook on net48. `Collectible` remains
  explicit when a test must name the ALC (and throws on net48).

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
Execution, MCP, NUnit, Revit, AutoCAD, or logging policy. Official UI sidecar
Share (`MahApps.Metro`, `ControlzEx`, `Microsoft.Xaml.Behaviors`) is identity
policy on the plan, not a UI framework dependency.

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
