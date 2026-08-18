# Assembly Isolation Kernel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract one reusable `DevTools.AssemblyIsolation` kernel and migrate every runtime/metadata assembly-loading feature to full-identity, explicit-parent-binding, lazy-resolution, lifecycle-safe behavior.

**Architecture:** The new multi-target leaf project owns identity matching, managed/native sources, stream loading, parent bindings, permanent/collectible/net48 lifecycles, metadata-only loading, and structured diagnostics. Execution, MCP, NUnit, add-in composition, and MTP retain their feature semantics and build immutable isolation plans; no feature names or logging dependencies enter the kernel.

**Tech Stack:** C# 14, net48/net8.0-windows/net10.0-windows, `AssemblyLoadContext`, `AssemblyDependencyResolver`, `MetadataLoadContext`, scoped `AppDomain.AssemblyResolve`, xUnit v3 MTP v2, NUnit host generation tests.

Date: 2026-08-18

Status: Active — design approved; implementation not started.

## Global Constraints

- Follow [decision 0023](../../decisions/0023-shared-assembly-isolation-kernel.md).
- Do not launch, locate, attach to, or contact Revit, AutoCAD, or Civil 3D during unit, discovery, or package verification.
- Do not default-share `System.*`, `Microsoft.*`, `Autodesk.*`, MahApps, ControlzEx, CommunityToolkit, or any other prefix.
- Parent unification accepts an actual `System.Reflection.Assembly` instance and validates full identity.
- Workload-local dependencies are lazy-loaded; do not preload sibling directories.
- Keep feature generation, registry, invocation, compilation, and package semantics outside the kernel.
- Preserve net48/net8/net10 behavior; do not add netstandard targets.
- Do not change ILRepack, Polyfill, package-public API, or host deployment policy in this refactor.
- Use `apply_patch` for source edits, preserve unrelated work, do not commit until the active task's review and GREEN evidence are recorded.

---

## Outcome And Recovery

At completion, all runtime assembly loads flow through `DevTools.AssemblyIsolation` or a documented feature adapter that only composes a kernel plan. `DevTools.Utilities` no longer owns assembly loading; MCP has no broad shared prefixes; command/MCP sibling dependencies are lazy; NUnit keeps generation isolation and Dynamo conflict protection through concrete parent bindings.

Each migration task is an independently reversible commit. If a feature parity gate fails, revert only that feature migration while retaining the already-green kernel; do not restore ambient name/prefix policies inside the kernel.

## Locked File Structure

```text
source/DevTools.AssemblyIsolation/
  DevTools.AssemblyIsolation.csproj
  AssemblyIsolationPlan.cs
  AssemblyIsolationSession.cs
  AssemblyIsolationLifecycle.cs
  Identity/AssemblyIdentityMatcher.cs
  Identity/AssemblyIdentityMismatchException.cs
  Bindings/ParentAssemblyBinding.cs
  Bindings/ParentAssemblyBindings.cs
  Diagnostics/AssemblyIsolationDiagnostic.cs
  Diagnostics/IAssemblyIsolationDiagnosticSink.cs
  Diagnostics/AssemblyUnloadResult.cs
  Sources/AssemblyCandidate.cs
  Sources/IManagedAssemblySource.cs
  Sources/INativeAssemblySource.cs
  Sources/ManifestAssemblySource.cs
  Sources/DirectoryAssemblySource.cs
  Sources/DependencyResolverAssemblySource.cs
  Sources/ManifestNativeAssemblySource.cs
  Sources/DependencyResolverNativeAssemblySource.cs
  Loading/AssemblyStreamLoader.cs
  Loading/PermanentAssemblyLoader.cs
  Loading/PermanentDirectoryAssemblyResolver.cs
  Runtime/CollectibleAssemblyIsolationContext.cs
  Runtime/NetFrameworkAssemblyIsolationScope.cs
  Metadata/MetadataAssemblySession.cs

tests/DevTools.AssemblyIsolation.Tests/
  DevTools.AssemblyIsolation.Tests.csproj
  AssemblyBoundaryTests.cs
  AssemblyIdentityMatcherTests.cs
  ParentAssemblyBindingTests.cs
  ManagedAssemblySourceTests.cs
  CollectibleSessionTests.cs
  NetFrameworkScopeContractTests.cs
  MetadataAssemblySessionTests.cs
  PermanentAssemblyLoaderTests.cs
  PermanentDirectoryAssemblyResolverTests.cs
  Fixtures/...
```

Feature policies stay with their consumers:

```text
source/DevTools.Execution/Providers/Dotnet/CommandIsolationPlan.cs
source/DevTools.Execution/Providers/CSharp/ScriptIsolationPlan.cs
source/DevTools.Mcp.Catalog/Discovery/McpToolsetIsolationPlan.cs
source/DevTools.NUnit.Host/Loading/NUnitIsolationPlan.cs
```

---

### Task 1: Establish The Leaf Project And Full-Identity Contract

**Files:**
- Create: `source/DevTools.AssemblyIsolation/DevTools.AssemblyIsolation.csproj`
- Create: `source/DevTools.AssemblyIsolation/Identity/AssemblyIdentityMatcher.cs`
- Create: `source/DevTools.AssemblyIsolation/Identity/AssemblyIdentityMismatchException.cs`
- Create: `source/DevTools.AssemblyIsolation/Bindings/ParentAssemblyBinding.cs`
- Create: `source/DevTools.AssemblyIsolation/Bindings/ParentAssemblyBindings.cs`
- Create: `source/DevTools.AssemblyIsolation/Diagnostics/AssemblyIsolationDiagnostic.cs`
- Create: `source/DevTools.AssemblyIsolation/Diagnostics/IAssemblyIsolationDiagnosticSink.cs`
- Create: `tests/DevTools.AssemblyIsolation.Tests/DevTools.AssemblyIsolation.Tests.csproj`
- Create: `tests/DevTools.AssemblyIsolation.Tests/AssemblyBoundaryTests.cs`
- Create: `tests/DevTools.AssemblyIsolation.Tests/AssemblyIdentityMatcherTests.cs`
- Create: `tests/DevTools.AssemblyIsolation.Tests/ParentAssemblyBindingTests.cs`
- Modify: `RevitDevTool.slnx`

**Interfaces:**
- Produces: `AssemblyIdentityMatcher.IsCompatible(AssemblyName requested, AssemblyName candidate)`.
- Produces: `ParentAssemblyBindings.Create(IEnumerable<Assembly> assemblies)` and `TryResolve(AssemblyName requested, out Assembly assembly)`.
- Produces: `IAssemblyIsolationDiagnosticSink.Publish(AssemblyIsolationDiagnostic diagnostic)`.
- Constraint: the project references only `System.Reflection.MetadataLoadContext`; it has no `DevTools.*`, MEL/ZLogger, WPF, or Autodesk reference.

- [ ] **Step 1: Add the test project and write RED contract tests**

```csharp
[Fact]
public void Parent_binding_returns_the_exact_compatible_instance()
{
    var expected = typeof(ParentAssemblyBindingTests).Assembly;
    var bindings = ParentAssemblyBindings.Create([expected]);

    Assert.True(bindings.TryResolve(expected.GetName(), out var actual));
    Assert.Same(expected, actual);
}

[Fact]
public void Parent_binding_rejects_same_name_with_different_version()
{
    var loaded = typeof(ParentAssemblyBindingTests).Assembly;
    var requested = new AssemblyName(loaded.FullName) { Version = new Version(99, 0, 0, 0) };
    var bindings = ParentAssemblyBindings.Create([loaded]);

    var error = Assert.Throws<AssemblyIdentityMismatchException>(
        () => bindings.TryResolve(requested, out _));
    Assert.Contains(loaded.GetName().Name!, error.Message, StringComparison.Ordinal);
}
```

Add culture/token/version cases and an architecture test that scans the source
project and compiled references for `Execution`, `Mcp`, `NUnit`, `RevitAPI`,
`acmgd`, `PresentationFramework`, `Microsoft.Extensions.Logging`, and `ZLogger`.

- [ ] **Step 2: Run RED**

Run:

```powershell
./scripts/test-dotnet.ps1 -Project tests/DevTools.AssemblyIsolation.Tests/DevTools.AssemblyIsolation.Tests.csproj
```

Expected: compile failure because the identity and binding types do not exist.

- [ ] **Step 3: Implement the minimal identity/binding kernel**

`IsCompatible` must compare name case-insensitively, exact requested version when
present, normalized neutral culture, and exact requested public-key token when
present. `ParentAssemblyBindings.Create` rejects duplicate simple names with
different full identities; it stores the actual assembly object and never scans
the AppDomain.

- [ ] **Step 4: Run GREEN and multi-target build**

```powershell
./scripts/test-dotnet.ps1 -Project tests/DevTools.AssemblyIsolation.Tests/DevTools.AssemblyIsolation.Tests.csproj
dotnet build source/DevTools.AssemblyIsolation/DevTools.AssemblyIsolation.csproj -c Debug
git diff --check
```

Expected: all tests pass; net48/net8/net10 build with zero warnings/errors.

- [ ] **Step 5: Review and commit**

```powershell
git add RevitDevTool.slnx source/DevTools.AssemblyIsolation tests/DevTools.AssemblyIsolation.Tests
git commit -m "feat(isolation): add assembly identity kernel"
```

---

### Task 2: Add Immutable Plans, Lazy Sources, And Structured Resolution

**Files:**
- Create: `source/DevTools.AssemblyIsolation/AssemblyIsolationLifecycle.cs`
- Create: `source/DevTools.AssemblyIsolation/AssemblyIsolationPlan.cs`
- Create: `source/DevTools.AssemblyIsolation/Sources/AssemblyCandidate.cs`
- Create: `source/DevTools.AssemblyIsolation/Sources/IManagedAssemblySource.cs`
- Create: `source/DevTools.AssemblyIsolation/Sources/INativeAssemblySource.cs`
- Create: `source/DevTools.AssemblyIsolation/Sources/ManifestAssemblySource.cs`
- Create: `source/DevTools.AssemblyIsolation/Sources/DirectoryAssemblySource.cs`
- Create: `source/DevTools.AssemblyIsolation/Sources/DependencyResolverAssemblySource.cs`
- Create: `source/DevTools.AssemblyIsolation/Sources/ManifestNativeAssemblySource.cs`
- Create: `source/DevTools.AssemblyIsolation/Sources/DependencyResolverNativeAssemblySource.cs`
- Create: `tests/DevTools.AssemblyIsolation.Tests/ManagedAssemblySourceTests.cs`
- Create: `tests/DevTools.AssemblyIsolation.Tests/AssemblyIsolationPlanTests.cs`

**Interfaces:**
- Consumes: `ParentAssemblyBindings` and `AssemblyIdentityMatcher` from Task 1.
- Produces:

```csharp
public enum AssemblyIsolationLifecycle { Permanent, Collectible, ScopedNetFramework }

public interface IManagedAssemblySource
{
    AssemblyCandidate? Resolve(AssemblyName requested);
}

public interface INativeAssemblySource
{
    AssemblyCandidate? Resolve(string unmanagedDllName);
}

public sealed record AssemblyCandidate(string Path, string SourceName, string AllowedRoot);

public sealed class AssemblyIsolationPlan
{
    public static AssemblyIsolationPlan Create(string entryAssemblyPath);
    public AssemblyIsolationPlan WithLifecycle(AssemblyIsolationLifecycle lifecycle);
    public AssemblyIsolationPlan BindToParent(Assembly assembly);
    public AssemblyIsolationPlan AddManagedSource(IManagedAssemblySource source);
    public AssemblyIsolationPlan AddNativeSource(INativeAssemblySource source);
    public AssemblyIsolationPlan WithDiagnosticSink(IAssemblyIsolationDiagnosticSink sink);
}
```

- [ ] **Step 1: Write RED plan/source tests**

Test that plan methods return a new instance without mutating the prior plan;
duplicate incompatible parent bindings fail during plan construction; manifest
lookup selects by full identity; directory lookup rejects traversal/out-of-root
candidates; duplicate compatible candidates are deterministic; and source
construction does not load an assembly.

Add explicit tests named:

```text
System_text_json_candidate_is_not_implicitly_shared
Microsoft_extensions_candidate_is_not_implicitly_shared
Directory_source_is_lazy_and_does_not_preload_siblings
Candidate_outside_allowed_root_is_rejected
```

- [ ] **Step 2: Run RED**

```powershell
./scripts/test-dotnet.ps1 -Project tests/DevTools.AssemblyIsolation.Tests/DevTools.AssemblyIsolation.Tests.csproj
```

Expected: missing plan/source types.

- [ ] **Step 3: Implement immutable plan and sources**

`ManifestAssemblySource` indexes identities once and fails on ambiguous
same-name/different-identity entries. `DirectoryAssemblySource` performs a
single lazy `{Name}.dll` probe per request. `DependencyResolverAssemblySource`
wraps `AssemblyDependencyResolver` only on `NET`; net48 plan composition uses
manifest/directory sources. All paths are normalized before containment checks.

- [ ] **Step 4: Run GREEN**

```powershell
./scripts/test-dotnet.ps1 -Project tests/DevTools.AssemblyIsolation.Tests/DevTools.AssemblyIsolation.Tests.csproj
dotnet build source/DevTools.AssemblyIsolation/DevTools.AssemblyIsolation.csproj -c Debug
```

- [ ] **Step 5: Review and commit**

```powershell
git add source/DevTools.AssemblyIsolation tests/DevTools.AssemblyIsolation.Tests
git commit -m "feat(isolation): add lazy resolution plans"
```

---

### Task 3: Implement Stream Loading And Truthful Lifecycle Semantics

**Files:**
- Create: `source/DevTools.AssemblyIsolation/AssemblyIsolationSession.cs`
- Create: `source/DevTools.AssemblyIsolation/Loading/AssemblyStreamLoader.cs`
- Create: `source/DevTools.AssemblyIsolation/Runtime/CollectibleAssemblyIsolationContext.cs`
- Create: `source/DevTools.AssemblyIsolation/Runtime/NetFrameworkAssemblyIsolationScope.cs`
- Create: `source/DevTools.AssemblyIsolation/Diagnostics/AssemblyUnloadResult.cs`
- Create: `tests/DevTools.AssemblyIsolation.Tests/CollectibleSessionTests.cs`
- Create: `tests/DevTools.AssemblyIsolation.Tests/NetFrameworkScopeContractTests.cs`
- Create: `tests/DevTools.AssemblyIsolation.Tests/Fixtures/IsolationEntry/...`
- Create: `tests/DevTools.AssemblyIsolation.Tests/Fixtures/PrivateSystemNamedDependency/...`

**Interfaces:**
- Consumes: immutable plan and ordered sources from Task 2.
- Produces:

```csharp
public sealed class AssemblyIsolationSession : IDisposable
{
    public static AssemblyIsolationSession Create(AssemblyIsolationPlan plan);
    public Assembly LoadEntryAssembly();
    public AssemblyUnloadResult VerifyUnload();
}

public sealed record AssemblyUnloadResult(bool IsCollectible, bool IsUnloaded, string? Detail);
```

- [ ] **Step 1: Write RED behavior tests**

Prove: parent-bound contract returns the same object; a workload-local
`System.*`-named fixture loads in the collectible context; an incompatible
parent binding fails before private fallback; stream loading leaves source DLL
writable; no sibling module initializer runs unless requested; native
candidates remain inside allowed roots; dispose releases callbacks and a weak
reference becomes dead after bounded GC attempts.

For net48, test that the resolver handler is registered only for the scope and
removed on dispose. Assert `VerifyUnload()` returns
`IsCollectible=false, IsUnloaded=false` rather than claiming unload.

- [ ] **Step 2: Run RED**

```powershell
./scripts/test-dotnet.ps1 -Project tests/DevTools.AssemblyIsolation.Tests/DevTools.AssemblyIsolation.Tests.csproj
```

- [ ] **Step 3: Implement the fixed algorithm**

The modern context performs exactly: parent binding → ordered private sources
→ CLR fallback. It validates candidate identity and root before
`AssemblyStreamLoader.Load`. It does not catch and discard load failures;
diagnostics include requested identity, source, candidate, and rejection reason.

The net48 scope owns one `ResolveEventHandler`, removes it idempotently, and
uses byte loading to avoid source locks. Do not introduce a generic MarshalByRef
contract in this task.

- [ ] **Step 4: Run GREEN and repeat unload test**

```powershell
./scripts/test-dotnet.ps1 -Project tests/DevTools.AssemblyIsolation.Tests/DevTools.AssemblyIsolation.Tests.csproj
./scripts/test-dotnet.ps1 -Project tests/DevTools.AssemblyIsolation.Tests/DevTools.AssemblyIsolation.Tests.csproj
dotnet build source/DevTools.AssemblyIsolation/DevTools.AssemblyIsolation.csproj -c Release
```

Expected: both test runs deterministic; all three TFMs build cleanly.

- [ ] **Step 5: Review and commit**

```powershell
git add source/DevTools.AssemblyIsolation tests/DevTools.AssemblyIsolation.Tests
git commit -m "feat(isolation): add collectible and net48 sessions"
```

---

### Task 4: Migrate Metadata Discovery And Permanent Add-In Loading

**Files:**
- Create: `source/DevTools.AssemblyIsolation/Metadata/MetadataAssemblySession.cs`
- Create: `source/DevTools.AssemblyIsolation/Loading/PermanentAssemblyLoader.cs`
- Create: `source/DevTools.AssemblyIsolation/Loading/PermanentDirectoryAssemblyResolver.cs`
- Create: `tests/DevTools.AssemblyIsolation.Tests/MetadataAssemblySessionTests.cs`
- Create: `tests/DevTools.AssemblyIsolation.Tests/PermanentAssemblyLoaderTests.cs`
- Create: `tests/DevTools.AssemblyIsolation.Tests/PermanentDirectoryAssemblyResolverTests.cs`
- Modify: `source/RevitDevTool/HostAdapters/RevitCommandDiscovery.cs`
- Modify: `source/AcadDevTool/HostAdapters/AcadCommandDiscovery.cs`
- Modify: `source/RevitDevTool/Application.cs`
- Modify: `source/AcadDevTool/Application.cs`
- Modify: `source/RevitDevTool/RevitDevTool.csproj`
- Modify: `source/AcadDevTool/AcadDevTool.csproj`
- Test: `tests/DevTools.Execution.Tests/...` discovery tests
- Test: `tests/DevTools.Hosting.Tests/...` add-in boundary tests

**Interfaces:**
- Produces: `MetadataAssemblySession.Create(string entryPath, IEnumerable<string> resolutionPaths)` and `LoadEntryAssembly()`.
- Produces: `PermanentAssemblyLoader.LoadPath(string assemblyPath)` with full-identity/path load-once behavior.
- Produces: `PermanentDirectoryAssemblyResolver.Create(string directory, PermanentAssemblyLoader loader)`, `Register()`, and idempotent `Dispose()` for process resolver hooks.
- Constraint: metadata session uses `MetadataLoadContext`; it never calls `Assembly.Load*` for target files.

- [ ] **Step 1: Write RED metadata/permanent tests**

Use a fixture with a module initializer that writes a marker file. Parse it via
`MetadataAssemblySession` and assert the marker does not exist. Test duplicate
metadata identities produce a deterministic error rather than
`PathAssemblyResolver` order dependence.

Test permanent loader identity/path caching and changed-file diagnostic without
claiming hot reload. Test resolver registration/disposal idempotence, managed
directory probing, and unmanaged lookup.

- [ ] **Step 2: Run RED**

```powershell
./scripts/test-dotnet.ps1 -Project tests/DevTools.AssemblyIsolation.Tests/DevTools.AssemblyIsolation.Tests.csproj
```

- [ ] **Step 3: Implement and migrate the two command discovery adapters**

Replace direct `MetadataLoadContext` construction in Revit/Acad command
discovery. Keep their Revit/AutoCAD type interpretation in host adapters. Add
the new project reference to both host projects.

- [ ] **Step 4: Migrate add-in startup**

Give each host `Application` a `PermanentDirectoryAssemblyResolver` field.
Create/register it from the add-in contents directory during startup and dispose
it during shutdown. Do not pass host API names. Preserve native DLL probing and
one-time startup behavior.

- [ ] **Step 5: Run GREEN**

```powershell
./scripts/test-dotnet.ps1 -Project tests/DevTools.AssemblyIsolation.Tests/DevTools.AssemblyIsolation.Tests.csproj
dotnet run --project tests/DevTools.Execution.Tests/DevTools.Execution.Tests.csproj -- --no-progress --filter-class '*CommandMetadataIsolationTests'
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -m:1
dotnet build source/AcadDevTool/AcadDevTool.csproj -c Debug.Autodesk.2025 -p:DeployAutoCadBundle=false -m:1
```

Expected: discovery remains metadata-only; builds do not launch/deploy hosts.

- [ ] **Step 6: Review and commit**

```powershell
git add source/DevTools.AssemblyIsolation source/RevitDevTool source/AcadDevTool tests
git commit -m "refactor(isolation): unify metadata and add-in loading"
```

---

### Task 5: Migrate Command And C# Script Execution

**Files:**
- Create: `source/DevTools.Execution/Providers/Dotnet/CommandIsolationPlan.cs`
- Create: `source/DevTools.Execution/Providers/CSharp/ScriptIsolationPlan.cs`
- Modify: `source/DevTools.Execution/DevTools.Execution.csproj`
- Modify: `source/DevTools.Execution/Providers/CSharp/CSharpCompiler.cs`
- Modify: `source/RevitDevTool/HostAdapters/RevitCommandRunner.cs`
- Modify: `source/AcadDevTool/HostAdapters/AcadCommandRunner.cs`
- Delete after GREEN: `source/DevTools.Execution/Providers/Dotnet/CommandLoadContext.cs`
- Delete after GREEN: `source/DevTools.Execution/Providers/CSharp/ScriptLoadContext.cs`
- Test: `tests/DevTools.Execution.Tests/AssemblyIsolation/...`

**Interfaces:**
- `CommandIsolationPlan.Create(entryPath, IEnumerable<Assembly> parentBindings)` composes dependency-resolver plus sibling-directory sources, collectible lifecycle, and no preload.
- `ScriptIsolationPlan.Create(compiledEntryName, IEnumerable<string> nugetPaths, IEnumerable<Assembly> parentBindings)` uses an exact manifest source for selected NuGet DLLs.
- Host adapters pass actual boundary assemblies, for example Revit
  `typeof(IExternalCommand).Assembly` and AutoCAD's command/API assembly. No
  host-name strings enter the kernel.

- [ ] **Step 1: Write RED execution parity tests**

Add fixture graphs proving transitive sibling resolution, private conflicting
dependency versions, explicit parent contract identity, lazy unrequested
sibling behavior, source-file unlock, command result parity, and collectible
unload. Include a `System.*`-named fixture and a `Microsoft.Extensions.*`
version-drift fixture.

- [ ] **Step 2: Run RED**

```powershell
dotnet run --project tests/DevTools.Execution.Tests/DevTools.Execution.Tests.csproj -- --no-progress --filter-class '*CommandAssemblyIsolationTests' '*ScriptAssemblyIsolationTests'
```

- [ ] **Step 3: Implement feature plan factories and migrate consumers**

Keep command instantiation, Revit duck typing, API-object purge, Acad invocation,
and C# compilation cache outside the kernel. Dispose the kernel session only
after feature strong references are cleared. Route kernel diagnostics through
the feature's existing logger adapter.

- [ ] **Step 4: Run GREEN and host-project builds**

```powershell
dotnet run --project tests/DevTools.Execution.Tests/DevTools.Execution.Tests.csproj -- --no-progress --filter-class '*CommandAssemblyIsolationTests'
dotnet run --project tests/DevTools.Execution.Tests/DevTools.Execution.Tests.csproj -- --no-progress --filter-class '*ScriptAssemblyIsolationTests'
dotnet build source/DevTools.Execution/DevTools.Execution.csproj -c Debug
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -m:1
dotnet build source/AcadDevTool/AcadDevTool.csproj -c Debug.Autodesk.2025 -p:DeployAutoCadBundle=false -m:1
```

- [ ] **Step 5: Delete old contexts only after parity is green, scan, and commit**

```powershell
rg -n "CommandLoadContext|ScriptLoadContext|PreloadAssemblies" source tests
git add source/DevTools.Execution source/RevitDevTool source/AcadDevTool tests/DevTools.Execution.Tests
git commit -m "refactor(execution): use shared assembly isolation"
```

Expected scan: no active old context or eager-preload implementation.

---

### Task 6: Migrate MCP Runtime And Metadata Registry

**Files:**
- Create: `source/DevTools.Mcp.Catalog/Discovery/McpToolsetIsolationPlan.cs`
- Modify: `source/DevTools.Mcp.Catalog/DevTools.Mcp.Catalog.csproj`
- Modify: `source/DevTools.Mcp.Catalog/Discovery/McpToolsetContext.cs`
- Modify: `source/DevTools.Mcp.Catalog/Discovery/McpToolsetContextManager.cs`
- Modify: `source/DevTools.Mcp.Catalog/Discovery/McpAssemblyParser.cs`
- Modify: `source/DevTools.Mcp.Catalog/Discovery/MetadataAssemblyPathCollector.cs`
- Test: `tests/DevTools.Mcp.Tests/McpToolsetIsolationTests.cs`
- Test: `tests/DevTools.Mcp.Tests/McpAssemblyParserTests.cs`

**Interfaces:**
- `McpToolsetIsolationPlan.Create(path)` binds concrete MCP contract assemblies using `typeof(McpServer).Assembly` and the actual protocol assembly instances required by reflected signatures.
- It adds dependency-resolver and sibling-directory private sources, with no shared prefixes and no preload.
- `McpToolsetContextManager` retains path-keyed feature caching; each value owns an `AssemblyIsolationSession`.

- [ ] **Step 1: Write RED MCP tests**

Assert the source contains no `SharedPrefixes`, `System.`, or `Microsoft.`
sharing policy. Build fixture toolsets that use a private
`Microsoft.Extensions.*` version, share exact MCP contracts with the parent,
leave an unrequested sibling initializer untouched, unload after dispatcher
cache clear, and expose identical tool/resource descriptors before and after
migration.

- [ ] **Step 2: Run RED**

```powershell
dotnet run --project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj -- --no-progress --filter-class '*McpToolsetIsolationTests'
```

Expected: broad-prefix/preload assertions fail against the current context.

- [ ] **Step 3: Replace MCP's private ALC with a kernel session**

Keep registry caching, method resolution, schema building, and dispatcher cache
ordering in MCP. `Clear()` must clear dispatcher strong references before
disposing sessions. Bridge structured diagnostics to the existing MCP logger.

- [ ] **Step 4: Replace MCP metadata loading with `MetadataAssemblySession`**

Keep `McpAssemblyParser` attribute interpretation unchanged. Move only path
normalization, duplicate identity handling, and metadata context lifecycle to
the kernel.

- [ ] **Step 5: Run GREEN and scans**

```powershell
dotnet run --project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj -- --no-progress --filter-class '*McpToolsetIsolationTests' '*ParserIntegrationTests' '*McpSharedRuntimePackagingTests'
dotnet build source/DevTools.Mcp.Catalog/DevTools.Mcp.Catalog.csproj -c Debug
rg -n "SharedPrefixes|\"System\.\"|\"Microsoft\.\"|PreloadSiblingAssemblies|new MetadataLoadContext" source/DevTools.Mcp.Catalog
```

Expected scan: no runtime prefix sharing, preload, or direct metadata context.

- [ ] **Step 6: Review and commit**

```powershell
git add source/DevTools.Mcp.Catalog tests/DevTools.Mcp.Tests
git commit -m "refactor(mcp): use shared assembly isolation"
```

---

### Task 7: Migrate NUnit Modern Generations Without Weakening Isolation

**Files:**
- Create: `source/DevTools.NUnit.Host/Loading/NUnitIsolationPlan.cs`
- Modify: `source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj`
- Modify: `source/DevTools.NUnit.Host/Loading/NUnitRuntimeSessionFactory.cs`
- Modify: `source/DevTools.NUnit.Host/Loading/NUnitFrameworkHostShare.cs`
- Modify: `source/DevTools.NUnit.Host/Loading/NUnitGenerationCopyPlanner.cs`
- Modify: `source/DevTools.NUnit.Host/Loading/NUnitRuntimeUnloadVerifier.cs`
- Create: `source/DevTools.NUnit.Host/Loading/NUnitRuntimeSessionHandle.cs`
- Delete after GREEN: `source/DevTools.NUnit.Host/Loading/NUnitRuntimeLoadContext.cs`
- Delete after GREEN: `source/DevTools.NUnit.Host/Loading/NUnitGenerationManagedAssemblyIndex.cs`
- Delete after GREEN: `source/DevTools.NUnit.Host/Loading/NUnitGenerationNativeAssetResolver.cs`
- Test: `tests/DevTools.NUnit.Host.Tests/NUnitGenerationBuilderTests.cs`
- Test: `tests/DevTools.NUnit.Host.Tests/NUnitAssemblyIsolationTests.cs`

**Interfaces:**
- `NUnitIsolationPlan.Create(NUnitGenerationManifest manifest, Assembly frameworkAssembly)` binds the exact framework assembly and `typeof(ITestingRuntimeSession).Assembly`, then adds manifest managed/native sources.
- NUnit generation/shadow creation remains unchanged and supplies immutable paths.
- `NUnitRuntimeSessionHandle` owns the kernel session and clears the runtime proxy before disposal/unload verification.

- [ ] **Step 1: Freeze current NUnit behavior with RED migration tests**

Cover exact private version selection, ambiguous identity rejection, path
containment, native asset ambiguity, source-file unlock, attachment/result
parity, neutral contract identity, and unload. Add an explicit fixture where a
private `System.Reflection.Metadata` or `Microsoft.Extensions.*` dependency
differs from the host copy and must stay generation-private.

Add Dynamo-conflict proof: preload a conflicting `nunit.framework`, have
`NUnitFrameworkHostShare` select the generation shadow framework, pass that
actual assembly to the plan, and assert runtime/test framework identity is the
selected object rather than the conflicting copy.

- [ ] **Step 2: Run RED**

```powershell
dotnet run --project tests/DevTools.NUnit.Host.Tests/DevTools.NUnit.Host.Tests.csproj -- --no-progress --filter-class '*NUnitAssemblyIsolationTests'
```

- [ ] **Step 3: Compose the NUnit plan and replace the modern ALC**

Preload/select `nunit.framework` in NUnit feature code before building the plan.
Do not add NUnit/Dynamo hooks to the kernel. Replace NUnit's managed index and
native resolver with manifest sources. Retain NUnit-specific generation IDs,
runtime activation, exception translation, and test session lifecycle.

- [ ] **Step 4: Remove ambient host-shared exclusion**

Generation copying may keep all workload dependencies. Runtime unification is
controlled only by concrete parent bindings. Remove calls to
`HostSharedAssemblies`, `NUnitSharedAssemblyPolicy`, and simple-name parent
lookup after equivalent tests are green.

- [ ] **Step 5: Run GREEN**

```powershell
./scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Host.Tests/DevTools.NUnit.Host.Tests.csproj
./scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Runtime.Tests/DevTools.NUnit.Runtime.Tests.csproj
dotnet build source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj -c Debug
```

- [ ] **Step 6: Review and commit**

```powershell
git add source/DevTools.NUnit.Host tests/DevTools.NUnit.Host.Tests tests/DevTools.NUnit.Runtime.Tests
git commit -m "refactor(nunit): use shared assembly isolation"
```

---

### Task 8: Migrate NUnit net48 And Remove Legacy Utility Ownership

**Files:**
- Modify: `source/DevTools.NUnit.Host/Loading/NetfxNUnitRuntimeSessionFactory.cs`
- Modify: `source/DevTools.NUnit.Host/Loading/NetfxNUnitGeneration.cs`
- Delete after GREEN: `source/DevTools.NUnit.Host/Loading/NetfxNUnitSharedAssemblyResolver.cs`
- Delete after GREEN: `source/DevTools.NUnit.Host/Loading/NUnitSharedAssemblyResolver.cs`
- Delete after GREEN: `source/DevTools.NUnit.Host/Loading/NUnitSharedAssemblyPolicy.cs`
- Delete: `source/DevTools.Utilities/AssemblyLoading/ByteAssemblyLoader.cs`
- Delete: `source/DevTools.Utilities/AssemblyLoading/DirectoryAssemblyLoader.cs`
- Delete: `source/DevTools.Utilities/AssemblyLoading/HostAssemblyResolver.cs`
- Delete: `source/DevTools.Utilities/AssemblyLoading/HostPackagePrefixes.cs`
- Delete: `source/DevTools.Utilities/AssemblyLoading/HostSharedAssemblies.cs`
- Delete: `source/DevTools.Utilities/AssemblyLoading/HostSharedAssemblyNames.cs`
- Delete: `source/DevTools.Utilities/AssemblyLoader.cs`
- Modify: `source/DevTools.Utilities/DevTools.Utilities.csproj`
- Modify: `tests/DevTools.Utilities.Tests/UtilitiesAssemblyBoundaryTests.cs`
- Delete/move: `tests/DevTools.Utilities.Tests/AssemblyLoadingTests.cs`
- Delete/move: `tests/DevTools.Utilities.Tests/HostSharedAssemblyPolicyTests.cs`
- Test: `tests/DevTools.NUnit.Host.NetFramework.Tests/NetFrameworkGenerationTests.cs`

**Interfaces:**
- net48 NUnit keeps its child AppDomain and feature MarshalByRef proxy.
- Inside that AppDomain it uses kernel identity/source logic and a scoped
  resolver; AppDomain creation/unload remains NUnit-owned in this refactor.
- No source outside `DevTools.AssemblyIsolation` may define general-purpose
  byte/directory loading or ambient shared-name policy after this task.

- [ ] **Step 1: Add RED net48 parity and architecture tests**

Test actual child-AppDomain unload, handler cleanup, private dependency version
selection, concrete neutral-contract identity, conflicting framework behavior,
and no WPF/system/microsoft prefix policy. Add repository scans for the deleted
Utilities and NUnit loader types.

- [ ] **Step 2: Run RED**

```powershell
./scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Host.NetFramework.Tests/DevTools.NUnit.Host.NetFramework.Tests.csproj
```

- [ ] **Step 3: Migrate net48 resolution and delete old utility ownership**

Preserve NUnit's child AppDomain boundary. Use the kernel scope/sources inside
it. Move reusable tests to `DevTools.AssemblyIsolation.Tests`; delete tests that
assert ambient `HostSharedAssemblies.Use` or prefix behavior. Remove startup
calls and composition classes `RevitHostApiAssemblies`/
`AcadHostApiAssemblies` if no consumer remains.

- [ ] **Step 4: Run GREEN and active-code scans**

```powershell
./scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Host.NetFramework.Tests/DevTools.NUnit.Host.NetFramework.Tests.csproj
./scripts/test-dotnet.ps1 -Project tests/DevTools.Utilities.Tests/DevTools.Utilities.Tests.csproj
rg -n --glob '!**/bin/**' --glob '!**/obj/**' "HostSharedAssemblies|HostSharedAssemblyNames|HostPackagePrefixes|ByteAssemblyLoader|DirectoryAssemblyLoader|NUnitSharedAssemblyPolicy"
```

Expected: only explicit negative architecture-test strings or historical docs.

- [ ] **Step 5: Build all touched TFMs and commit**

```powershell
dotnet build source/DevTools.AssemblyIsolation/DevTools.AssemblyIsolation.csproj -c Release
dotnet build source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj -c Release
dotnet build source/DevTools.Utilities/DevTools.Utilities.csproj -c Release
git diff --check
git add source tests RevitDevTool.slnx
git commit -m "refactor(isolation): remove ambient assembly loaders"
```

---

### Task 9: Audit Remaining Loaders, Package Boundaries, And Whole Solution

**Files:**
- Modify: `source/RevitDevTool/Execution/PyRevit/PyRevitAssemblyLoader.cs`
- Preserve and document exception: `source/DevTools.NUnit.Mtp/MtpRuntimeAssemblyResolver.cs`
- Create: `tests/DevTools.AssemblyIsolation.Tests/RepositoryAssemblyLoadingArchitectureTests.cs`
- Modify: `source/DevTools.NUnit.Host/build/NUnitHostPackaging.targets`
- Modify: `source/DevTools.NUnit.Runtime/build/NUnitRuntimePayload.targets`
- Modify: `docs/agents/host-boundaries.md`
- Move after completion: `docs/plans/active/2026-08-18-assembly-isolation-kernel.md` to `docs/plans/completed/`

**Interfaces:**
- Repository architecture test allowlists only feature adapters that compose
  `AssemblyIsolationPlan` or metadata sessions.
- Direct `AssemblyLoadContext` subclasses, `AssemblyResolve` subscriptions,
  `Assembly.LoadFile`, `Assembly.Load(byte[])`, and `MetadataLoadContext`
  construction outside the kernel require an explicit documented exception.

- [ ] **Step 1: Inventory remaining direct loader APIs and write RED guard**

```powershell
rg -n --glob '!**/bin/**' --glob '!**/obj/**' "class .*: AssemblyLoadContext|AssemblyResolve \+=|Assembly\.LoadFile|Assembly\.Load\(File\.ReadAllBytes|new MetadataLoadContext|LoadFromStream\(" source
```

Turn the expected post-migration allowlist into
`RepositoryAssemblyLoadingArchitectureTests`. The test must scan `.cs`,
`.csproj`, `.props`, `.targets`, solution, and packaging inputs.

- [ ] **Step 2: Migrate PyRevit and preserve the MTP bootstrap exception**

Keep PyRevit's year/TFM candidate selection in the feature, but replace its
direct `Assembly.Load(byte[])` and simple-name loaded cache with
an application-lifetime `PermanentAssemblyLoader.LoadPath`. Add a focused test
proving two same-name/different-identity candidates are not silently treated as
the same loaded assembly.

Keep `MtpRuntimeAssemblyResolver` as the only direct `AssemblyResolve` exception:
the packaged MTP assembly must locate its private runtime closure before it can
load and call the kernel, so depending on the kernel here creates a bootstrap
cycle. Add this exact file to the architecture-test allowlist and assert it is
limited to `AppContext.BaseDirectory`, registers once, and contains no shared
prefix or feature execution logic.

- [ ] **Step 3: Verify payload ownership**

Assert `DevTools.AssemblyIsolation.dll` is merged or shipped exactly once
according to each host/package boundary, never copied into an NUnit generation
as a second identity, and never exposed as public compile API by
`RevitDevTool.NUnit` unless explicitly intended. Do not change ILRepack flags.

- [ ] **Step 4: Run focused suite matrix**

```powershell
./scripts/test-dotnet.ps1 -Project tests/DevTools.AssemblyIsolation.Tests/DevTools.AssemblyIsolation.Tests.csproj
dotnet run --project tests/DevTools.Execution.Tests/DevTools.Execution.Tests.csproj -- --no-progress --filter-class '*CommandAssemblyIsolationTests' '*ScriptAssemblyIsolationTests'
dotnet run --project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj -- --no-progress --filter-class '*McpToolsetIsolationTests' '*ParserIntegrationTests' '*McpSharedRuntimePackagingTests'
./scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Host.Tests/DevTools.NUnit.Host.Tests.csproj
./scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Host.NetFramework.Tests/DevTools.NUnit.Host.NetFramework.Tests.csproj
./scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Runtime.Tests/DevTools.NUnit.Runtime.Tests.csproj
./scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Mtp.Tests/DevTools.NUnit.Mtp.Tests.csproj
```

- [ ] **Step 5: Run build/package matrix without host launch**

```powershell
dotnet build source/DevTools.AssemblyIsolation/DevTools.AssemblyIsolation.csproj -c Release
dotnet build source/DevTools.Execution/DevTools.Execution.csproj -c Release
dotnet build source/DevTools.Mcp.Catalog/DevTools.Mcp.Catalog.csproj -c Release
dotnet build source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj -c Release
dotnet build source/DevTools.NUnit.Mtp/DevTools.NUnit.Mtp.csproj -c Release
dotnet build RevitDevTool.slnx -c Debug.Autodesk.2025 -m:1 -p:UseSharedCompilation=false -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false
```

Run the existing clean-consumer NUnit package matrix and inspect host outputs for
one kernel identity, no legacy Utilities loader, no copied host API, and no
duplicate Testing contract.

- [ ] **Step 6: Whole-change review**

Review dependency direction, thread safety, event cleanup, full-identity
matching, path traversal, native resolution, unload strong roots, net48 truth,
MCP descriptor parity, NUnit Dynamo conflict proof, packaging, and docs. Fix all
Critical/Important findings and rerun affected proof.

- [ ] **Step 7: Update the single architecture truth and complete the plan**

Update `docs/agents/host-boundaries.md` so “Assembly load” points to
`DevTools.AssemblyIsolation` and lists feature plan adapters. Do not duplicate
the full decision. Record exact commands/results and remaining limitations in
this plan, move it to `docs/plans/completed/`, then commit:

```powershell
git add docs source tests RevitDevTool.slnx
git commit -m "docs(isolation): record shared assembly kernel"
```

## Validation Summary Required Before Completion

- Kernel tests: identity, parent binding, private System/Microsoft drift,
  allowed roots, lazy loading, native resolution, source unlock, diagnostics,
  permanent/collectible/net48 lifecycle, metadata no-execution.
- Feature tests: command/C# result parity, MCP descriptor/invocation/cache
  parity, NUnit generation/result/cancellation/unload/Dynamo conflict parity.
- Architecture scan: no ambient host-shared registry, broad shared prefixes,
  eager sibling preload, or duplicate general-purpose loader.
- Build: kernel, Execution, MCP Catalog, NUnit Host/MTP across applicable TFMs;
  full Autodesk 2025 solution with deployment disabled.
- Packaging: one kernel identity, no host API runtime copies, no duplicate
  neutral contracts, no change to ILRepack/Polyfill policy.
- Live host execution is not required for extraction completion unless a unit or
  package gate cannot prove the touched behavior; any missing host evidence must
  be reported explicitly rather than inferred.

## Progress

- [ ] Task 1: leaf project and identity contract.
- [ ] Task 2: immutable plans and lazy sources.
- [ ] Task 3: stream loading and lifecycle.
- [ ] Task 4: metadata and permanent add-in migration.
- [ ] Task 5: command and C# script migration.
- [ ] Task 6: MCP migration.
- [ ] Task 7: NUnit modern migration.
- [ ] Task 8: NUnit net48 and legacy cleanup.
- [ ] Task 9: remaining-loader audit and whole verification.

## Decisions

- 2026-08-18: Use concrete parent `Assembly` bindings; do not migrate ambient
  host names or shared prefixes.
- 2026-08-18: CLR fallback owns runtime/framework resolution only after private
  dependency sources return no candidate.
- 2026-08-18: Keep feature caches/generation/invocation outside the kernel.
- 2026-08-18: Treat net48 default-AppDomain scope cleanup separately from true
  unload; preserve NUnit child AppDomain isolation.

## Result

Pending implementation and verification.
