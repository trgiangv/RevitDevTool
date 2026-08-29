# 0023 Shared Assembly Isolation Kernel

Date: 2026-08-18

## Status

Accepted

**Amendment 2026-08-29.** Public matcher stays `AssemblyIdentityMatcher.IsCompatible`
(full identity; optional `allowVersionDrift` for Share). net48 isolated resolve
additionally allows a **newer** candidate for `System.Text.Json`,
`Microsoft.Bcl.AsyncInterfaces`, `System.IO.Pipelines`, and
`System.Text.Encodings.Web` (`NetfxBclBind`) so TUnit.Engine’s STJ 9 request
binds the CPM 10 payload. CoreCLR paths stay exact. Do not unify on net8/net10
and do not change the public matcher API.

## Context

RevitDevTool currently implements assembly loading independently for the add-in
deploy folder, .NET command execution, C# scripts, MCP toolsets, metadata-only
discovery, PyRevit integration, the MTP private runtime, and NUnit generations.
The implementations repeat stream loading, directory probing, identity checks,
native resolution, event registration, and unload handling, but do not apply the
same rules.

The most important drift is ownership selection. NUnit keeps versioned
`System.*` and `Microsoft.*` dependencies generation-private and validates full
assembly identity. MCP currently treats both prefixes as shared and command
loading preloads every sibling DLL. Several net48 paths use process-wide
`AssemblyResolve` hooks with feature-specific cleanup behavior. This creates
different DLL-hell and unload outcomes for equivalent workloads.

Host API packages are compile-only (`ExcludeAssets="runtime"`) and are already
loaded by Revit or AutoCAD. A reusable kernel therefore does not need an ambient
host-name registry or `Autodesk.*` prefix policy. Cross-context identity can be
expressed directly with the actual parent `Assembly` instance.

## Decision

Create `DevTools.AssemblyIsolation`, targeting
`net48;net8.0-windows;net10.0-windows`, as the host-neutral leaf kernel for:

- managed and native dependency resolution;
- full assembly identity comparison;
- explicit parent-context binding;
- path and manifest candidate validation;
- stream/PDB loading without source locks;
- permanent, collectible, and scoped-net48 lifecycle handling;
- metadata-only loading; and
- structured resolution and unload diagnostics.

The project has no dependency on Execution, MCP, NUnit, Revit/AutoCAD APIs,
WPF, or a logging framework. Consumers translate its diagnostics to their own
logging surface.

### Parent-context binding

The kernel does not expose `HostSharedAssemblies`, host-name lists, or shared
prefixes. An isolation plan binds only concrete assemblies that must preserve
type identity:

```csharp
var plan = AssemblyIsolationPlan.Create(entryPath)
    .BindToParent(typeof(IHostContextExecutor).Assembly)
    .BindToParent(typeof(ITestingRuntimeSession).Assembly);
```

Bindings are keyed and validated by full identity: simple name, version,
culture, and public-key token. The session returns the exact bound `Assembly`
instance. A request with the same simple name but an incompatible identity
fails; it never selects the first AppDomain assembly with that name.

Host APIs may be bound by a feature only when their types cross the isolation
boundary. Evidence-backed global-state/resource assemblies such as the
DevTools MahApps/ControlzEx sidecars may also be bound explicitly. No
`System.*`, `Microsoft.*`, `Autodesk.*`, or UI-package prefix is implicit.

NUnit remains responsible for loading its generation-selected
`nunit.framework` into the parent context, then passes that concrete assembly
to the kernel as an ordinary parent binding. The kernel does not know NUnit or
Dynamo.

### Resolution precedence

For each requested identity, a session performs these steps:

1. Return an explicitly bound parent assembly when its full identity is
   compatible; reject an incompatible same-name binding.
2. Query the plan's ordered private dependency sources.
3. Validate that a candidate is inside its allowed root and has a compatible
   full identity, then stream-load it into the isolated context.
4. When no source produces a candidate, return unresolved and let the CLR
   binder handle runtime/framework assemblies.

This makes a workload-local `System.Text.Json`,
`System.Reflection.Metadata`, or `Microsoft.Extensions.*` assembly private
when it is present. The kernel does not maintain a runtime inventory and does
not default-share namespace prefixes.

Dependency sources are lazy. Sibling directories are not preloaded. A feature
may add a manifest, `AssemblyDependencyResolver`, directory, loaded-instance,
or native source, but feature code does not override the load algorithm.

### Lifecycle

- `Permanent` owns the add-in deploy-directory resolver and load-once cache.
  It does not claim reload support after an assembly identity has been loaded.
- `Collectible` owns a CoreCLR `AssemblyLoadContext`, event cleanup, strong
  reference release, `Unload()`, and weak-reference verification.
- `ScopedNetFramework` owns scoped `AssemblyResolve` registration and cleanup,
  but does not claim that assemblies loaded into the default AppDomain unload.
  NUnit net48 uses this lifetime in the host's default AppDomain: load the
  generation shadow with `LoadFile` so same-identity copies stay distinct,
  reuse already-loaded host APIs, and accept that Revit cannot unload those
  DLLs. Do not create a child AppDomain for NUnit generations.
- Metadata discovery uses a separate `MetadataAssemblySession`. It uses
  `MetadataLoadContext`, never executes the target assembly, and never creates
  a runtime isolation session.

### Ownership boundary

The kernel does not own:

- NUnit generation snapshots, hashes, retention, or test lifecycle;
- MCP registry/schema/dispatcher caches;
- command discovery semantics or command invocation;
- C# compilation or NuGet selection;
- host API composition;
- MTP protocol or package layout; or
- ILRepack/Polyfill policy.

Each feature builds an isolation plan, owns its higher-level cache and semantic
lifecycle, and consumes a kernel session.

## Consequences

Positive:

- one full-identity and resolution algorithm applies across features;
- workload-local `System.*` and `Microsoft.*` packages are no longer
  accidentally replaced by host versions;
- cross-context contracts use concrete assembly instances instead of ambient
  global name policy;
- collectible contexts expose deterministic cleanup and unload evidence;
- metadata discovery remains code-free and host-free; and
- new assembly-backed features extend the kernel through dependency sources
  and plan composition instead of creating another load context.

Tradeoffs:

- feature migrations must preserve their current observable semantics before
  old loaders can be deleted;
- net48 default-AppDomain loading still cannot unload individual assemblies;
- binding a parent assembly is an explicit feature decision and must be tested;
  and
- NUnit's generation and framework selection remain intentionally more complex
  than the generic kernel.

## Rejected Alternatives

1. Move existing loaders unchanged into a new project. This changes ownership
   without removing policy drift.
2. Put NUnit generation and MCP/Execution caches into the kernel. This creates
   a feature-aware god loader.
3. Share `System.*`, `Microsoft.*`, or `Autodesk.*` by prefix. Prefixes do not
   prove ownership or version compatibility.
4. Search the AppDomain by simple name. It silently accepts version/token
   drift and ambiguous identities.
5. Treat all exact identities already loaded in the parent as shared. Separate
   contexts may intentionally require independent static state even for the
   same version; parent binding must remain explicit.
6. Create a child AppDomain for every NUnit net48 generation. Autodesk API
   objects cannot cross that boundary safely, mixed-mode host APIs cannot be
   unloaded, and the origin path already isolated same-identity copies with
   shadow `LoadFile` in the default domain. See [0016](0016-nunit-native-runtime-and-mtp-first-integration.md).

## Validation

- architecture tests prove the kernel is a leaf and contains no feature,
  host, WPF, or logging dependency;
- identity tests cover version, culture, public-key token, ambiguity, and
  incompatible same-name parent bindings;
- private resolution tests prove workload-local `System.*` and `Microsoft.*`
  candidates remain isolated;
- lifecycle tests prove event cleanup, source-file unlock, and collectible
  unload behavior without claiming net48 default-AppDomain unload;
- metadata tests prove module initializers are not executed;
- feature parity suites cover add-in, Execution, MCP, NUnit modern/net48, and
  package/host payload boundaries; and
- repository scans find no remaining runtime loader outside the kernel unless
  documented as a feature adapter.

## Implemented Boundary

The accepted decision is implemented across add-in startup and discovery,
command/script execution, MCP toolsets, PyRevit and PythonNet metadata tooling,
and NUnit modern/net48 runtimes. The supported observable contract is maintained
in [the product document](../product/assembly-isolation.md); implementation
scope and verification are recorded in the
[completed plan](../plans/completed/2026-08-18-assembly-isolation-kernel.md).

The MTP package bootstrap remains the only documented direct resolver outside
the kernel because it must locate the private provider closure before the kernel
itself can load. It is restricted to the application base directory and exact
full identities.

## Related Decisions

- [0016](0016-nunit-native-runtime-and-mtp-first-integration.md)
- [0018](0018-host-identity-and-out-of-process-infrastructure.md)
- [0019](0019-ilrepack-and-polyfill-isolated-alc.md)
- [0021](0021-testing-kernel-and-provider-owned-framework-runtime.md)
- [0022](0022-nunit-mtp-only-testing-stack.md)
