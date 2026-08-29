# Execution Plan: Testing Core Open-Closed For Providers

Date: 2026-08-22

## Status

Active — Tasks 0–4 landed; Opus 5 gate **Accept** (B1, S1–S8 closed).

**Current names (2026-08-29):** MSBuild `MTPAssembly` / `MTPEntry` / `MTPCopy` /
`TestingRunnerPath`; copy `CopyMTPSibling`; type `HostMtpRegistration`; merge
`MergeTestConfig`. JSON keys stay camelCase: `mtpAssembly`, `mtpEntry`. Truth:
[0024](../../decisions/0024-testing-core-open-closed-providers.md) amendment and
[`docs/architecture/Testing/README.md`](../../architecture/Testing/README.md).
Do not reintroduce `DevToolsMTP*` properties.

## Outcome

A third in-host test provider can be added without editing
`DevTools.Testing.Abstractions` or `DevTools.Testing.Host.csproj`. NUnit and
TUnit keep current testhost / in-host behavior. Sonar findings on
`ValidatePlan` and unused store setters are gone. Testhost never
`TypeInitializationException`s on missing config. Partial config fails as a
discovery **and** run error node.

## Context

- Decision: [0024](../../decisions/0024-testing-core-open-closed-providers.md)
  (closes unimplemented 0021 §1; reviewed twice 2026-08-22)
- Structure: [docs/architecture/Testing/README.md](../../architecture/Testing/README.md)
- Product: [host-testing.md](../../product/host-testing.md),
  [tunit-host-testing.md](../../product/tunit-host-testing.md)
- Agent trap page: [agents/host-testing.md](../../agents/host-testing.md)

## Scope

In scope: 0024 §1–4 plus architecture README rows named in Task 4.

Out of scope: new xUnit provider; Host assembly split; deleting
`TestingProviderPayload`; narrowing `IHostTestDiscoverer`; TUnit UID /
NUnit `RunMapping`; ILRepack/Polyfill; changing pack restore TFM rules;
nested JSON parse in `MergeDevToolsTestConfig`.

## Approach

Task 0 (store) → Task 1 (public files) → Task 2 (DI) run as one Host/provider
slice. Task 3 (testhost config + MSBuild) is a parallel TestAdapter slice.
Do not edit the other slice’s files. Task 4 (docs) after both compile+test.

| Slice | Tasks | Owns (do not cross) |
|-------|-------|---------------------|
| Host/provider | 0, 1, 2 | `DevTools.Testing.Host`, `DevTools.NUnit.Host`, `DevTools.TUnit.Host`, `RevitServiceRegistration.cs`, `AcadServiceRegistration.cs`, matching Host/NUnit.Host tests |
| Testhost | 3 | `DevTools.Testing.Abstractions`, `DevTools.TestAdapter`, `NUnitMTP.cs` / `TUnitMTP.cs` crefs, Adapter + Abstractions tests |
| Docs | 4 | `docs/architecture/Testing/README.md`, `docs/agents/host-testing.md` |

Bump `DevTools.TestAdapter.csproj` `<Version>` only when publishing (`0.0.3`
today; accidental bumps in the TUnit spike were reverted).
Git revert does not un-publish a nupkg.

### Task 0 — Store validation and dead seams

**Files**

- `source/DevTools.Testing.Host/Loading/TestingGenerationStore.cs`
- `source/DevTools.Testing.Host/Loading/TestingGenerationPlan.cs` (add
  `ValidateShape` on the existing record file)
- `source/DevTools.Testing.Host/Loading/TestingGenerationBuilder.cs` (delete)
- `source/DevTools.Testing.Host/Runtime/TestingRuntimeSessionManager.cs`
- `tests/DevTools.Testing.Host.Tests/Loading/TestingGenerationStoreTests.cs`
- `tests/DevTools.Testing.Host.Tests/GenerationBuilderTests.cs` (delete)
- `tests/DevTools.NUnit.Host.Tests/NUnitGenerationTestEnvironment.cs`
- `tests/DevTools.NUnit.Host.Tests/Loading/NUnitRuntimeTestEnvironment.cs`
- `tests/DevTools.NUnit.Host.NetFramework.Tests/NetFrameworkGenerationTestEnvironment.cs`

**Work**

1. `TestingGenerationPlan.ValidateShape()`: empty framework id, empty files,
   rooted/`..` relative paths, duplicate normalized paths, runtime relative
   path not in the set. No I/O.
2. Store `ValidateSources(plan, testAssemblyPath)`: source exists, matches
   request, each `SourcePath` exists.
3. Delete `BeforeFileCopied`, `BeforePublish`, empty `Dispose`/`IDisposable`.
4. Keep `AfterFileCopied` and both coherence retries. Remove the third
   `ComputeGenerationId` (`TestingGenerationStore` ~line 76).
5. Delete `TestingGenerationBuilder`. Fix `using TestingGenerationStore` and
   the three NUnit test environments.

**Proof**

- Host.Tests generation tests pass; new shape tests without temp dirs.
- NUnit host test environments compile.
- Sonar complexity of remaining validate methods under 10.

### Task 1 — Public generation file API

**Files**

- `source/DevTools.Testing.Host/Loading/TestingGenerationFiles.cs`
- `source/DevTools.Testing.Host/DevTools.Testing.Host.csproj` (drop
  InternalsVisibleTo NUnit.Host / TUnit.Host)
- `source/DevTools.NUnit.Host/Loading/NUnitGenerationCopyPlanner.cs`
- `source/DevTools.NUnit.Host/Loading/NUnitGenerationPolicy.cs` (version
  check; currently calls `ValidateManagedFrameworkVersion`)
- `source/DevTools.TUnit.Host/TUnitGenerationPolicy.cs`
- `tests/DevTools.Testing.Host.Tests/Loading/TestingGenerationFilesTests.cs`

**Work**

1. Public: `Classify`, `ScanOutputDirectory`, `IsSharedTestingContract`,
   `TryGetManagedAssemblyIdentity`, `IsManagedAssembly`, `TryGetFileVersion`,
   `ContentEquals`, `MergeFile` (add or replace-if-content-differs). Callers
   may mutate the scan dictionary.
2. **Delete** `ValidateManagedFrameworkVersion`. NUnit and TUnit policies
   inline version + managed checks and throw their own exception types.
3. Do **not** add `MergeRuntime(HostRuntimeSource, …)` or
   `RuntimeMergeConflict`. Providers keep destination names:
   `NUnitGenerationPolicy.RuntimeAssemblyFileName` /
   `RuntimeSymbolFileName`; `TUnitGenerationPolicy.RuntimeAssemblyFileName`.
4. NUnit planner keeps `GenerationCopyEntry` and
   `IsRuntimeOwnedFileName`. Do **not** switch `ValidateNUnitFramework` to
   `ScanOutputDirectory` (unfiltered enumerate must still see a duplicate
   under `TestResults\`). Copy inclusion may keep `TryIncludeOutputFile`.
5. TUnit keeps skipping `IsSharedTestingContract` on runtime files. NUnit
   does **not** gain that skip on runtime dependencies.

**Proof**

- Build NUnit.Host + TUnit.Host all TFMs.
- Host.Tests: one conflict-case pin (replace if content differs) plus
  `ContentEquals`.
- Grep: no `InternalsVisibleTo` for those two hosts; no
  `ValidateManagedFrameworkVersion`.

### Task 2 — Provider DI idiom

**Files**

- `source/DevTools.NUnit.Host/NUnitHostingExtensions.cs`
- `source/DevTools.NUnit.Host/NUnitHostTestFrameworkProvider.cs`
- `source/DevTools.NUnit.Host/Loading/NUnitRuntimeSessionFactory.cs`
- `source/DevTools.TUnit.Host/TUnitHostingExtensions.cs`
- `source/RevitDevTool/Composition/RevitServiceRegistration.cs`
- `source/AcadDevTool/Composition/AcadServiceRegistration.cs`
- New test: descriptor count independent of NUnit/TUnit registration order
- Grep: `GetRequiredService<TestingGenerationStore>`,
  `ITestingRuntimeSessionFactory`, `ITestingGenerationPolicy`

**Work**

1. `TryAddEnumerable` for NUnit `IHostTestFrameworkProvider`.
2. Stop unkeyed `TryAddSingleton` of `ITestingGenerationPolicy`,
   `TestingGenerationStore`, `TestingRuntimeSessionManager`,
   `ITestingRuntimeSessionFactory`.
3. Provider field: one store under `%TEMP%\DevTools\NUnit\Generations` for
   process lifetime. Mirror `TUnitHostTestFrameworkProvider`.
4. Test both registration orders; AutoCAD still resolves `nunit` only.

**Proof**

- Focused Host/NUnit.Host tests; both composition roots compile.

### Task 3 — Testhost plugin configuration (single fail-closed change)

**MSBuild properties (public)**

| Property | Written to | First-party default when empty |
|----------|------------|--------------------------------|
| `TestingFramework` | `devtools.frameworkId` | `nunit` (props, already) |
| `DevToolsMTPAssembly` | `devtools.mtpAssembly` | `nunit` → `DevTools.NUnit.MTP.dll`; `tunit` → `DevTools.TUnit.MTP.dll` |
| `DevToolsMTPEntry` | `devtools.mtpEntry` | `nunit` → `DevTools.NUnit.MTP.NUnitMTP`; `tunit` → `DevTools.TUnit.MTP.TUnitMTP` |

`<Error>` if `DevToolsMTPAssembly` or `DevToolsMTPEntry` is empty after the
map.

**User `testconfig.json` algorithm (no nested JSON parse)**

1. Generate always includes the three plugin keys from the properties.
2. No user file / no `"devtools"` substring: existing splice of generated
   `devtools` into other top-level user keys.
3. User file contains `"devtools"`: write-through only if the user text also
   contains `"frameworkId"`, `"mtpAssembly"`, and `"mtpEntry"`; else
   `<Error>` (delete `devtools` or add the keys).

**`mtpAssembly` load rule**

Reject unless `Path.GetFileName(value) == value` (not rooted, no directory
separator). Missing file or rejected name → `LastError`, no
`LoadUnlocked`.

**Run-path error surface**

Move `EnsureSession()` and `RequireDiscoverer()` **inside**
`PublishRunAsync`. Catch config/plugin failures and publish
`TestNodeProperties.CreateErrorNode` (same as `PublishDiscoveredAsync`).
`ExecuteRequestAsync` must not throw for partial config.

**Files**

- `source/DevTools.Testing.Abstractions/Config/HostTestConfig.cs` (keys
  `MTPAssembly`, `MTPEntry`; **delete** `DefaultFrameworkId`)
- Delete `source/DevTools.Testing.Abstractions/Mtp/HostMTPRegistration.cs`
- Create `source/DevTools.TestAdapter/HostMTPRegistration.cs`
- `source/DevTools.TestAdapter/AdapterBootstrap.cs` (no throw; do not call
  `RequireFrameworkId()` from the hook static ctor)
- `source/DevTools.TestAdapter/TestingPlatformBuilderHook.cs` (static ctor
  risk; drop Abstractions cref)
- `source/DevTools.TestAdapter/AdapterTestConfig.cs`
- `source/DevTools.TestAdapter/HostOptionsLoader.cs` (empty frameworkId
  fails)
- `source/DevTools.TestAdapter/HostTestSession.cs` (empty frameworkId fails)
- `source/DevTools.TestAdapter/HostTestFramework.cs` (message from configured
  `mtpAssembly`; run catch as above)
- `source/DevTools.NUnit.MTP/NUnitMTP.cs` (cref — MTP cannot reference
  Adapter; point at `HostTestDiscovery`)
- `source/DevTools.TUnit.MTP/TUnitMTP.cs` (same)
- `source/DevTools.TestAdapter/DevTools.TestAdapter.csproj` (version bump)
- `source/DevTools.TestAdapter/build/RevitDevTool.TestAdapter.props` (map +
  Error)
- `source/DevTools.TestAdapter/build/RevitDevTool.TestAdapter.targets`
  (`GenerateDevToolsTestConfig`; `MergeDevToolsTestConfig` user-devtools
  Error; `CopyDevToolsTestAdapterRuntimeClosure` exclude all
  `DevTools.*.MTP.dll` then include exactly `$(DevToolsMTPAssembly)`;
  rename `CopyDevToolsNUnitMtp` → `CopyDevToolsMTPSibling`; in-repo
  `ProjectReference` Exists blocks)
- `tests/DevTools.TestAdapter.Tests/AdapterArchitectureTests.cs` (invert:
  Abstractions must **not** contain MTP DLL names; must **not** contain
  `DefaultFrameworkId` in `HostOptionsLoader`; do not call
  `TryResolvePlugin("nunit")` on Abstractions; `CopyDevToolsMTPSibling`;
  Abstractions `HostMTPRegistration.cs` path must not be read)
- `tests/DevTools.Testing.Abstractions.Tests/HostMTPCatalogTests.cs` → move
  / rewrite under Adapter tests; missing plugin sets `LastError`
- `tests/DevTools.Testing.Abstractions.Tests/AssemblyBoundaryTests.cs`
  (remove `IsPlugInContractFile` exemptions for `MTP/` and
  `Config/HostTestConfig.cs`)
- `tests/DevTools.TestAdapter.Tests/HostTestSessionTests.cs` (drop
  `DefaultFrameworkId`; discovery message from config; empty frameworkId
  fails)
- `tests/DevTools.TestAdapter.Tests/PackageConsumerTests.cs` (positive: all
  three keys; negative: frameworkId-only → discovery **and** run error
  node, **no** static-ctor throw)
- Stub plugin test: `TestingFramework=fake` +
  `DevToolsMTPAssembly` / `DevToolsMTPEntry` that assigns
  `HostTestDiscovery.Provider`

**Work**

1. Targets write the three keys from the named properties.
2. First-party map + Error as in the table.
3. No `frameworkId`-only dual-read.
4. Closure + sibling copy as in 0024 §1.

**Proof**

- Architecture tests + PackageConsumerTests + HostTestSessionTests +
  AssemblyBoundaryTests.
- Stub `fake` plugin (Open/Closed).
- `scripts/pack-test-adapter.ps1` still builds both MTP TFMs.
- `dotnet test --list-tests` NUnit sample and TUnit sample.

### Task 4 — Docs (architecture + agent trap)

**Files**

- `docs/architecture/Testing/README.md`: source-map row “MTP plugin load”
  (Adapter, not Abstractions); bootstrap table = testconfig keys + MSBuild
  property names (`DevToolsMTPAssembly`, `DevToolsMTPEntry`), not a C#
  switch / DLL table at current lines 127–130; generation files public
  member list from 0024 §3; enumerable DI; fail-closed static ctor; run
  error node
- `docs/agents/host-testing.md`: missing `mtpAssembly`; user
  `devtools` without plugin keys is a build Error
- Product pages only if documenting the two override properties.

**Proof**

- Architecture README does not claim plugin load lives in Abstractions.

## Risks And Recovery

- **Testhost 0 tests:** static ctor never throws; `LastError` → discovery
  and run error nodes.
- **Stale / user testconfig:** build Error if `"devtools"` lacks plugin
  keys; no nested merge.
- **`mtpAssembly` path escape:** reject non-bare names before
  `Path.Combine`.
- **Nupkg:** Task 3 version bump; prior package has no keys. Rollback =
  publish previous adapter version, not only `git revert`.
- **DI:** grep leftover `GetRequiredService<TestingGenerationStore>()`.
- Task 0/1 are forward-compatible with old bootstrap.

## Progress

- [x] Task 0 — store validation split; delete dead hooks and GenerationBuilder
- [x] Task 1 — public generation files; drop provider InternalsVisibleTo
- [x] Task 2 — enumerable providers; NUnit owns store and session factory
- [x] Task 3 — testconfig MTP keys; loader in Adapter; version bump
- [x] Task 4 — architecture Testing README + agent trap

## Decisions

- 2026-08-22: Fail closed if `mtpAssembly`/`mtpEntry` missing. No fallback
  switch. No `frameworkId`-only dual-read.
- 2026-08-22: Keep `TestingProviderPayload` on the wire (0021 IPC). Delete
  unused store setters (not on the wire).
- 2026-08-22: User `"devtools"` without plugin keys is a build Error. No
  nested JSON merge in the inline task.
- 2026-08-22: Kernel merge is per-file `MergeFile` / `ContentEquals`. No
  `RuntimeMergeConflict`. Skip filters stay provider-owned.
- 2026-08-22: Public MSBuild overrides are `DevToolsMTPAssembly` and
  `DevToolsMTPEntry`. Rename `CopyDevToolsNUnitMtp` →
  `CopyDevToolsMTPSibling`. C#/MSBuild use `MTP`, never `Mtp`. JSON keys
  stay `mtpAssembly` / `mtpEntry`.
- 2026-08-22: Path helpers live on `TestingGenerationFiles`;
  `TestingGenerationPaths` stays internal (not a second public Host type).
- 2026-08-22: Unmapped `DevToolsMTPAssembly` does not MSB3030. Sibling
  `<Error>` stands down when the DLL is already in `OutDir` or
  `DevToolsMTPCopy=false`. Proof: `FakeMTPCopyTests`.

## Validation

- Focused: Host.Tests generation; AdapterArchitectureTests;
  HostTestSessionTests; PackageConsumerTests; AssemblyBoundaryTests;
  NUnit.Host + TUnit.Host compile; DI order test; `fake` plugin positive;
  missing-keys discovery **and** run error nodes; NUnit host test
  environments compile
- Integration: `--list-tests` on both samples; one in-host smoke each if a
  host year is available
- Repository: compile touched csproj per `.agents/skills/build/SKILL.md`

## Result

Opus 5 gate **Accept**. Focused: Host.Tests 50/50, Adapter.Tests 63/63
(including `FakeMTPCopyTests`). Unmapped `TestingFramework=fake` copy
succeeds when the DLL is already in `OutDir`; sibling Error still fires
when it is missing. Live `--list-tests` / in-host smoke not run in this
session.
