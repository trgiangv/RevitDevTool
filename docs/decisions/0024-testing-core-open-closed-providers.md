# 0024 Testing Core Open-Closed For Providers

Date: 2026-08-22

## Status

Accepted. Refines [0021](0021-testing-kernel-and-provider-owned-framework-runtime.md)
after an independent review of the implemented kernel versus NUnit and TUnit
providers. Does not replace 0021’s testhost / in-host split.

**Amendment 2026-08-29.** Public MSBuild names dropped the `DevTools` prefix:
`MTPAssembly`, `MTPEntry`, `MTPCopy`, `TestingRunnerPath`. Copy is one target
(`CopyMTPSibling`) from `build/runtime`; Ipc/Transport are ILRepacked into the
adapter. Testhost plugin type is `HostMtpRegistration`. In-host `testing/*`
is `MarshaledTestRequestHandler` → `DotnetTestRequestHandler`. Merge task is
`MergeTestConfig`. JSON keys are unchanged (`mtpAssembly`, `mtpEntry`).

Reviewed twice on 2026-08-22 (SOLID / fail-closed / YAGNI). Second pass closed
prior B1–B6 in this text and added the MSBuild property names, copy targets,
`mtpAssembly` path rule (JSON camelCase `mtp*`; MSBuild `MTPAssembly` /
`MTPEntry`; type `HostMtpRegistration`), run-path error node, user-`devtools`
restriction, and the per-file merge helper (no `RuntimeMergeConflict` enum).

## Context

0021 §1 required opaque framework IDs, no NUnit constant in Abstractions, and
no MTP/reflection catalog in that assembly. That clause was **not implemented
as accepted**. The kernel still fails Open/Closed: adding a third provider
requires editing core. This ADR closes that gap; it is not new drift.

| Leak | Location | Why it is a leak |
|------|----------|------------------|
| `switch` `nunit` / `tunit` | `HostMTPRegistration.TryResolvePlugin` | Catalog in Abstractions |
| `RequiredMtpAssembliesMessage` | same type; used by `HostTestFramework` | First-party DLL names in Abstractions |
| `DefaultFrameworkId = "nunit"` | `HostTestConfig`; **also** `HostOptionsLoader` / `HostTestSession` substitute empty run `frameworkId` | C# default; run path fail-open to `nunit` |
| `InternalsVisibleTo` NUnit.Host / TUnit.Host | `DevTools.Testing.Host.csproj` | `TestingGenerationFiles` is kernel API |
| `'$(TestingFramework)' == 'tunit'` / `!= 'tunit'` on **copy/exclude** | `CopyDevToolsTestAdapterRuntimeClosure` (netcoreapp) and `CopyDevToolsNUnitMtp` (net48 + in-repo MTP sibling) | `!= tunit` means “NUnit”; a third `DevTools.*.MTP.dll` in `build/runtime` is copied by both closure branches |

Two composition idioms exist for the same kernel: NUnit
`TryAddSingleton<IHostTestFrameworkProvider, …>` plus unkeyed
`TestingGenerationStore` / `ITestingRuntimeSessionFactory`; TUnit
`TryAddEnumerable` and owns its store in the provider. `TryAddSingleton`
skips if any descriptor for that service type already exists. Host composition
currently registers NUnit first; reversing order silently drops NUnit.

`TestingGenerationStore` grew speculative seams (`BeforeFileCopied`,
`BeforePublish`, empty `IDisposable`) and a `ValidatePlan` method that mixes
pure plan-shape rules with filesystem checks.

The testhost must remain host-free. In-host execute must stay on
`IHostContextExecutor`. `HostTestDiscovery` static handoff must stay: net48
ILRepack leaves Abstractions as the one shared type identity (CS0433).

## Decision

### 1. Plugin load is configuration, not a C# catalog

`DevTools.Testing.Abstractions` must not contain provider assembly file names,
entry type names, a `switch` on `nunit` / `tunit`, or a required-assemblies
message that names first-party DLLs.

Teshost bootstrap reads opaque strings from `testconfig.json` (written from
the test csproj). **Naming:** JSON keys stay camelCase to match `frameworkId`.
MSBuild properties use `MTP` (`MTPAssembly`, `MTPEntry`). The testhost type is
`HostMtpRegistration`.

- `devtools.frameworkId` (required on **discovery and run**; no C# default)
- `devtools.mtpAssembly` (bare file name beside the testhost, required)
- `devtools.mtpEntry` (public type with static `Register()`, required)

C# keys: `HostTestConfig.Keys.MTPAssembly` / `MTPEntry` (string values
`"mtpAssembly"` / `"mtpEntry"`).

`HostMtpRegistration` **moves** to `DevTools.TestAdapter` (the process that
loads sibling DLLs). Abstractions keeps only `HostTestDiscovery.Provider` /
`RunMapper`. `LastError` stays a process-global static on that moved type:
one testhost process loads one plugin; the move does not invent a better
handoff, it only stops leaking first-party names into Abstractions.

Default `TestingFramework=nunit` stays in `RevitDevTool.TestAdapter.props`
only.

#### Named MSBuild contract

Public properties a consumer or in-repo provider may set (empty means “apply
the first-party map”):

| Property | Meaning |
|----------|---------|
| `TestingFramework` | Opaque id written as `devtools.frameworkId`. Props default `nunit`. |
| `MTPAssembly` | Bare file name written as `devtools.mtpAssembly`. |
| `MTPEntry` | Type name written as `devtools.mtpEntry`. |

First-party map in `.props`, applied only when the assembly/entry properties
are still empty:

| `TestingFramework` | `MTPAssembly` | `MTPEntry` |
|--------------------|---------------|------------|
| `nunit` | `DevTools.NUnit.MTP.dll` | `DevTools.NUnit.MTP.NUnitMTP` |
| `tunit` | `DevTools.TUnit.MTP.dll` | `DevTools.TUnit.MTP.TUnitMTP` |

Build `<Error>` when `MTPAssembly` or `MTPEntry` is empty
after that map (unknown `TestingFramework` with no overrides).

Honest Open/Closed:

- **Abstractions and `DevTools.Testing.Host.csproj` do not change** when a
  provider is added.
- **Teshost plugin load** is open given the three keys plus a DLL already
  beside the testhost. Packaged copy of a first-party MTP is gated on
  `Exists(...)`. The sibling `<Error>` stands down when
  `$(OutDir)$(MTPAssembly)` is already present, or when
  `MTPCopy=false`.
- **In-repo** providers still edit packaged `.props`/`.targets` (map row,
  in-repo `ProjectReference` Exists block, MTP output fallback) and the host
  composition roots (`RevitServiceRegistration`, `AcadServiceRegistration`).
- **Third-party** providers are not copied by the nupkg. They place the DLL
  in `OutDir` themselves (or opt out of copy). First-party gates
  (`ValidateDevToolsTUnitTarget`, TUnit package refs, builder-hook Remove)
  may remain.

#### Fail-closed, observable on discovery **and** run

Missing or partial keys must **not** throw from
`TestingPlatformBuilderHook`’s static constructor
(`TypeInitializationException` aborts testhost with “0 Tests found”).
`AdapterBootstrap.Initialize` captures into `LastError` and skips
`Register`.

Discovery already catches and publishes `TestNodeProperties.CreateErrorNode`
(`HostTestFramework.PublishDiscoveredAsync`). Run must do the same:

- `EnsureSession()` / `HostOptionsLoader.Load` / `RequireDiscoverer()` must
  run **inside** `PublishRunAsync` (today `EnsureSession()` is an argument to
  that method, and `RequireDiscoverer()` sits outside its `try`).
- Empty `frameworkId` must fail the load, not substitute `nunit`.
- Catch those failures and publish an error node; do not let them escape
  `ExecuteRequestAsync` as an unhandled exception.

`mtpAssembly` is a **bare file name**: `Path.GetFileName(value) == value`,
no directory separator, not rooted. Otherwise set `LastError` and do not
call `Path.Combine` / `LoadUnlocked`. Missing file also sets `LastError`.

#### MSBuild copy

`CopyMTPSibling` copies `$(MTPAssembly)` and
`DevTools.Testing.Abstractions.dll` from `build/runtime` (nupkg layout; in-repo
`_StagePackageRuntime` stages that folder). Ipc/Transport are ILRepacked into
the adapter on every TFM — do not copy a first-party closure glob. Do not Error
when the MTP DLL is already in `$(OutDir)` or `MTPCopy=false`. Do not use
`!= tunit` to mean NUnit.

#### User-authored `testconfig.json`

`MergeTestConfig` is a `RoslynCodeTaskFactory` fragment with no JSON
parser. Nested object merge is not an allowed algorithm.

Rule:

- No user file, or user file with no `"devtools"` substring: write generated
  `devtools` (including the three plugin keys), splicing other top-level user
  keys as today.
- User file contains `"devtools"`: write-through **only if** the user text
  already contains `"mtpAssembly"` and `"mtpEntry"` (and `"frameworkId"`).
  Otherwise `<Error>`: delete the `devtools` section or add the three keys.
  User values win. No brace-splicing into a nested object.

### 2. One provider composition idiom

Every `IHostTestFrameworkProvider` registers with `TryAddEnumerable`.

The provider instance owns its `TestingGenerationStore`,
`TestingRuntimeSessionManager`, `ITestingGenerationPolicy`, and
`ITestingRuntimeSessionFactory`. The kernel must not be registered as unkeyed
singletons from a provider extension.

Invariant: the count of `IHostTestFrameworkProvider` descriptors is independent
of registration order. Cover Revit (NUnit + TUnit) and AutoCAD (NUnit only).

### 3. Generation file helpers are public kernel API

Public members of `TestingGenerationFiles` (Host):

- `Classify`
- `ScanOutputDirectory` — returns `Dictionary<string, TestingGenerationFile>`;
  callers may mutate it
- `IsSharedTestingContract` — required public (NUnit planner and TUnit policy
  already call it)
- `TryGetManagedAssemblyIdentity`
- `IsManagedAssembly`
- `TryGetFileVersion(string path, out string? fileVersion)`
- `ContentEquals(string firstPath, string secondPath)`
- `MergeFile(IDictionary<string, TestingGenerationFile> files, string sourcePath, string relativePath)` —
  add, or replace **only if content differs**. Destination `relativePath` is
  chosen by the caller (runtime assembly / symbol names stay provider
  constants).
- `NormalizeRelativePath`, `GetRelativePath`, `IsVolatileGenerationOutput` —
  net48-safe path helpers. `TestingGenerationPaths` stays **internal**.

Do **not** add `MergeRuntime(HostRuntimeSource, …)` or a `RuntimeMergeConflict`
enum. NUnit maps `runtimeSource.AssemblyPath` →
`NUnitGenerationPolicy.RuntimeAssemblyFileName` (and symbols); TUnit maps to
`TUnitGenerationPolicy.RuntimeAssemblyFileName` with no symbols. Those names
cannot be derived in the kernel. NUnit may keep `GenerationCopyEntry` and use
`ContentEquals` / `MergeFile` after a local dictionary, or keep its list and
only share `ContentEquals`.

Skip rules stay provider-owned:

- NUnit: `IsRuntimeOwnedFileName` (includes `nunit.framework.dll`). NUnit
  does **not** skip `IsSharedTestingContract` on runtime *dependencies*
  today; do not silently adopt TUnit’s skip.
- TUnit: skip `IsSharedTestingContract` on runtime files, as today.

`ValidateManagedFrameworkVersion` is **deleted** from the kernel. Each
provider inlines version + managed checks using `TryGetFileVersion` /
`IsManagedAssembly` and throws its own exception type. Do not publish a
kernel method that takes `Func<string, Exception>`.

`InternalsVisibleTo` for NUnit.Host and TUnit.Host is removed.

NUnit **validation** of “exactly one `nunit.framework.dll`” keeps an
unfiltered `Directory.EnumerateFiles`. `ScanOutputDirectory` must not replace
that scan (it would hide a duplicate under `TestResults\`).

### 4. Delete speculative store seams; split validation

Remove `BeforeFileCopied`, `BeforePublish`, and the empty `Dispose` /
`IDisposable` on `TestingGenerationStore`. Keep `AfterFileCopied`. Check
`TestingRuntimeSessionManager` (holds the store), tests that `using` the
store, and the NUnit host test environments that construct the store.

Delete `TestingGenerationBuilder` and `GenerationBuilderTests` (second store
path with unused `GenerationLocks`; only tests construct it).

`TestingGenerationPlan.ValidateShape()` (no I/O) lives on the existing record
file `TestingGenerationPlan.cs`. Store keeps filesystem and caller-identity
checks. Remove the third `ComputeGenerationId` that only existed for
`BeforePublish`.

Do not add new Action/event hooks on the store unless a caller lands in the
same change. The same YAGNI bar forbids a one-value merge-policy enum.

### 5. What this decision does not change

- Host-free testhost vs in-host `testing/run`.
- `HostTestDiscovery` static assignment (net48 identity).
- Coherent generation retry, content-hash publish, `GenerationLocks` on the
  store.
- `MarshaledTestRequestHandler` vs `DotnetTestRequestHandler`.
- `TestingDiscoveryHints` (optional; TUnit consumes, NUnit ignores).
- `TestingProviderPayload` on the wire (reserved from 0021). Removing it is a
  published IPC break; unused store setters are not on the wire, so they
  delete under §4.
- NUnit `RunMapping`, `NUnitFrameworkHostShare`, retirement diagnostics.
- TUnit Engine UID expansion.
- Splitting Host so generation has no IPC references (deferred).

## Alternatives Considered

1. **Keep the C# `switch` and add cases per provider.** Rejected: Open/Closed
   violation; Abstractions becomes a registry of first-party names.
2. **Convention `DevTools.{Id}.MTP.dll` from `frameworkId` alone.** Rejected:
   `tunit` vs `TUnit` casing is not a function of the id string.
3. **Keep `InternalsVisibleTo` instead of public generation files.** Rejected:
   friend assemblies are not an extension mechanism.
4. **Keep NUnit `TryAddSingleton` for the provider.** Rejected: order-dependent
   silent drop.
5. **Fail-open fallback switch when `mtpAssembly` is missing.** Rejected:
   reintroduces the catalog.
6. **`MergeRuntime(HostRuntimeSource, …, RuntimeMergeConflict)`.** Rejected:
   cannot name destination files; enum would have one used value; skip/filter
   rules differ and must stay in the provider.
7. **Nested JSON merge of user `devtools` in the inline task.** Rejected: no
   parser on that task; string surgery is not a merge.
8. **Split Host so generation kernel has no IPC/bridge references.** Deferred.

## Consequences

Positive:

- A third **in-repo** provider can add Host + MTP + Runtime without editing
  Abstractions or Host.csproj. Testhost load uses the named MSBuild
  properties. Pack authoring and composition roots may still change.
- One DI story; composition order is not a product invariant.
- Generation scan/merge is documented kernel behavior (per-file, no policy
  enum).
- Store cognitive load drops.

Tradeoffs:

- `testconfig.json` grows two keys. User-authored `devtools` without those
  keys becomes a **build error**.
- Packaged `.props`/`.targets` need a TestAdapter version bump; reverting git
  does not un-publish a nupkg.
- Public `TestingGenerationFiles` is a Host API surface; keep the member list
  in this ADR and the architecture README.
- First-party `.props` host-support / package-ref gates may still mention
  `tunit`; that is pack authoring, not the Abstractions catalog.

## Follow-Up

- Implementation: `docs/plans/completed/2026-08-22-testing-core-open-closed.md`.
- After land: `docs/architecture/Testing/README.md` source-map row, bootstrap
  table, generation section; agent trap for missing `mtpAssembly` / user
  `devtools` Error.
- Guardrail: architecture tests fail if `DevTools.Testing.Abstractions`
  contains `NUnit.MTP` or `TUnit.MTP` strings. Remove
  `AssemblyBoundaryTests.IsPlugInContractFile` exemptions for `MTP/` and
  `Config/HostTestConfig.cs`.
- Proof of Open/Closed: stub `TestingFramework=fake` with
  `MTPAssembly` / `MTPEntry` loads a testhost plugin
  (`HostMTPRegistrationTests`); MSBuild copy succeeds when that DLL is
  already in `OutDir` (`FakeMTPCopyTests`). Missing keys → discovery
  **and** run error nodes, no static-ctor throw.
