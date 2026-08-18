# Assembly isolation

RevitDevTool loads add-ins, commands, scripts, MCP toolsets, and NUnit runtimes
without allowing one feature's dependency policy to leak into another.
`DevTools.AssemblyIsolation` is the single shared kernel for this behavior.

## Contract

- Parent sharing is explicit and uses a concrete `Assembly` instance.
- Compatibility uses full identity: name, version, culture, and public-key
  token. A matching simple name is not sufficient.
- Private managed and native dependencies resolve lazily from roots declared by
  the owning feature. Directory traversal and reparse-point escapes are rejected.
- `System.*`, `Microsoft.*`, Autodesk APIs, and UI libraries are not shared by
  prefix. When a feature needs shared type identity it binds that exact assembly.
- Unresolved framework dependencies fall back to the CLR after private sources
  have declined the request.
- Metadata discovery never executes the inspected assembly.

## Lifetimes

| Lifetime | Product behavior |
|----------|------------------|
| Permanent | Add-in-shipped assemblies are path-loaded once for the process lifetime so WPF resources and dependency locations remain stable. They are not hot-reloaded. |
| Collectible | Commands, scripts, MCP toolsets, and modern NUnit generations release feature references before unloading their context. |
| Scoped net48 | Resolver hooks are registered only for the owning scope. Default-AppDomain assemblies do not claim unload support. |
| NUnit net48 | Same default AppDomain as the host. Generation **shadow** copies are `LoadFile`'d so the same identity can exist per generation (hot reload) without locking the project output. Host CAD APIs already loaded by Revit/AutoCAD are reused in place. Those DLLs cannot be unloaded from the process. |

Feature code continues to own compilation, discovery semantics, registries,
generation snapshots, invocation, result mapping, and logging. It composes an
isolation plan and translates structured diagnostics; the kernel contains no
Execution, MCP, NUnit, Revit, AutoCAD, WPF, or logging policy.

## Packaging boundary

Autodesk API references are compile-only and are not copied as runtime payloads.
Host packages merge or ship one kernel identity according to their existing
ILRepack policy. `RevitDevTool.TestAdapter` exposes only its platform compile surface and
keeps its modern implementation closure private.

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
