# Execution Plan: Testing Kernel Extraction And NUnit Provider Isolation

Date: 2026-08-17

> **For agentic workers:** REQUIRED SUB-SKILL: use
> `superpowers:subagent-driven-development` and execute tasks sequentially.
> Use TDD for production changes. Do not commit; the user will review the full
> pending diff before authorizing commits.

## Status

Active

## Outcome

Items 1-6 of [Decision 0021](../../decisions/0021-testing-kernel-and-provider-owned-framework-runtime.md)
are implemented: the testing kernel and Runner core are framework-neutral,
NUnit owns its semantics and compatibility workarounds, source linking is gone,
and `DevTools.NUnit.Core` is removed. PolySharp remains unchanged for a later
reviewed phase.

## Architecture

`DevTools.Testing.Abstractions` is the only loose cross-load-context identity.
`DevTools.Testing.Host` implements policy-driven generation and session
mechanics. NUnit packages implement framework policy and semantics. The
`DevTools.TestRunner` executable composes a neutral Runner core with an NUnit
command module.

## Tech Stack

C# 14, .NET Framework 4.8, .NET 8/10 Windows, Microsoft Testing Platform 2.3.3,
NUnit 4.6.1, Named Pipes, collectible `AssemblyLoadContext`, net48 coherent
generation loading, xUnit v3 repository tests as MTP executables
(`dotnet run --project tests/…`).

## Global Constraints

- Discovery never locates, launches, attaches to, or contacts Revit/AutoCAD.
- Only execution may start `DevTools.TestRunner` and activate a host.
- `DevTools.Testing.*` must not reference or contain NUnit types, constants,
  protocol method names, framework versions, or DLL filenames.
- `DevTools.Testing.Abstractions` has no JSON, IPC, MTP, reflection, process,
  load-context, framework, or Autodesk dependency.
- Preserve NUnit 4.6.1 semantics and the current net48/modern runtime isolation
  behavior; do not replace native NUnit with reflection emulation.
- Preserve legacy `nunit/*` behavior under NUnit-owned compatibility code.
- Preserve one loose `DevTools.Testing.Abstractions.dll` identity across host
  and provider runtime contexts.
- Do not change `Polyfill`, add PolySharp, or change ILRepack flags/policy.
- Do not add xUnit/TUnit provider implementation.
- Do not commit. Leave all implementation and documentation changes pending.
- Use `dotnet run --project tests/<project>/<project>.csproj` for focused tests and the build
  skill for touched multi-target projects.

---

### Task 1: Complete Neutral Contracts Without Capability Loss

**Files:**

- Modify: `source/DevTools.Testing.Abstractions/Contracts/TestingContracts.cs`
- Modify: `source/DevTools.Testing.Abstractions/Providers/IHostTestFrameworkProvider.cs`
- Create: `source/DevTools.Testing.Abstractions/Runtime/ITestingRuntimeSession.cs`
- Modify: `source/DevTools.NUnit.Host/NUnitTestingMapper.cs`
- Modify: `source/DevTools.NUnit.Host/NUnitHostTestFrameworkProvider.cs`
- Modify: `tests/DevTools.Testing.Abstractions.Tests/ContractRoundTripTests.cs`
- Modify: `tests/DevTools.Testing.Abstractions.Tests/AssemblyBoundaryTests.cs`
- Modify: relevant NUnit Host mapper/provider tests.

**Interfaces:**

- `FrameworkId` is an opaque non-empty string; remove `TestingFrameworkIds`.
- Extend `TestingAttachment` with `ContentType`, `Path`, and `Base64` while
  retaining an optional description/name field.
- Extend `TestingCaseResult` with `ParentTestId`, `FullName`, `SkipReason`, and
  `TestingProviderPayload? ProviderPayload`.
- Add `TestingProviderPayload(string Format, int Version, string Data)`.
- Add neutral `ITestingRuntimeSession`, `ITestingRuntimeEventSink`, and
  `TestingRuntimeEvent`; the new runtime interface exposes `GenerationId`,
  `Run`, `Cancel`, and `Dispose`, but no discovery method.
- Define `NUnitFramework.Id = "nunit"` in NUnit-owned code and use it for the
  provider/mappers.

- [ ] Write failing contract round-trip tests for hierarchy, full name, skip
  reason, all attachment forms, and opaque provider payload.
- [ ] Write a failing architecture test rejecting `NUnit`, `nunit`, MTP, JSON,
  IPC, reflection, and process references in Abstractions source/assembly.
- [ ] Run the two new tests and confirm they fail for missing fields and the
  existing NUnit framework constant.
- [ ] Implement the contract extensions and neutral runtime interfaces.
- [ ] Update NUnit mapping so every old `NUnitCaseResult` field survives the
  neutral conversion; do not encode common fields inside provider payload.
- [ ] Update callers/tests for the provider-owned framework ID.
- [ ] Run Abstractions and NUnit Host focused tests and record counts.

### Task 2: Remove NUnit Compatibility From Generic Dispatch

**Files:**

- Modify: `source/DevTools.Testing.Host/TestingRequestHandler.cs`
- Modify: `source/DevTools.Testing.Host/TestingProviderRegistry.cs`
- Create or modify NUnit-owned legacy adapter files under
  `source/DevTools.NUnit.Host/`.
- Modify: `source/DevTools.NUnit.Host/NUnitHostingExtensions.cs`
- Modify: host composition registration as required.
- Modify: `tests/DevTools.Testing.Host.Tests/*`
- Modify: `tests/DevTools.NUnit.Host.Tests/*`
- Modify: `tests/DevTools.Execution.Tests/BridgeHandlerRegistrationTests.cs`

**Interfaces:**

- `TestingRequestHandler` supports exactly `testing/hello`, `testing/run`, and
  `testing/cancel`; `testing/discover` remains rejected without contacting a
  provider.
- NUnit-owned compatibility handler supports existing `nunit/*` methods and
  maps directly to NUnit legacy contracts/provider behavior.
- `TestingProviderRegistry` normalizes arbitrary IDs by trim/lowercase only.

- [ ] Write failing generic architecture/dispatch tests proving no `nunit/*`
  method or NUnit literal exists in `DevTools.Testing.Host`.
- [ ] Write failing NUnit compatibility tests proving legacy hello/run/cancel
  remain registered and produce the existing envelope.
- [ ] Run the new tests and verify expected failures.
- [ ] Move legacy parsing/routing into NUnit Host without changing wire DTOs.
- [ ] Remove compatibility flags, constants, method mapping, and NUnit defaults
  from generic dispatch and registry.
- [ ] Update DI/composition so generic and NUnit legacy handlers coexist.
- [ ] Run Testing Host, NUnit Host, and bridge-registration suites.

### Task 3: Extract The Common Generation And Runtime Kernel

**Files:**

- Add focused files under `source/DevTools.Testing.Host/Loading/` for
  generation plan/store/index/resolution/session management.
- Modify existing `TestingGeneration*` files rather than maintaining two
  implementations.
- Add runtime policy/factory interfaces under
  `source/DevTools.Testing.Host/Runtime/` or Abstractions when they cross ALC.
- Migrate mechanism tests from `tests/DevTools.NUnit.Host.Tests/Loading/` to
  `tests/DevTools.Testing.Host.Tests/Loading/` where no NUnit behavior is
  asserted.
- Preserve NUnit-specific tests in their current project.

**Interfaces:**

```csharp
public enum TestingGenerationFileKind { Managed, Native, Symbols, Other }

public sealed record TestingGenerationFile(
    string SourcePath,
    string RelativePath,
    TestingGenerationFileKind Kind);

public sealed record TestingGenerationPlan(
    string FrameworkId,
    string SourceAssemblyPath,
    IReadOnlyList<TestingGenerationFile> Files,
    string RuntimeAssemblyRelativePath);

public interface ITestingGenerationPolicy
{
    TestingGenerationPlan CreatePlan(string testAssemblyPath);
    void ValidatePublished(TestingGenerationManifest manifest);
}

public interface ITestingRuntimeSessionFactory
{
    ITestingRuntimeSession Create(TestingGenerationManifest generation);
}
```

- [ ] Move or write failing common tests for deterministic hash, source-change
  retry, corruption rejection, atomic publication, managed/native indexing,
  current/obsolete generation retirement, active-run cancellation, and
  retained-generation diagnostics.
- [ ] Confirm tests fail because the generic kernel lacks the mechanism.
- [ ] Implement `TestingGenerationPlan` consumption and coherent generation
  publication with no framework-specific filename/version assumptions.
- [ ] Extract common managed/native resolution and ALC/net48 loading mechanism
  behind provider resolution/activation policy.
- [ ] Extract common session manager lifecycle using neutral runtime contracts.
- [ ] Keep the old NUnit implementation temporarily until Task 4 reaches
  parity; do not delete proven behavior in this task.
- [ ] Run Testing Host tests on net10 and compile `DevTools.Testing.Host` for
  all target frameworks.

### Task 4: Rebuild NUnit Host As Policies Over The Common Kernel

**Files:**

- Modify or create NUnit policy/adapter files under
  `source/DevTools.NUnit.Host/Loading/`.
- Modify: `source/DevTools.NUnit.Host/NUnitRuntimeManager.cs`
- Modify: `source/DevTools.NUnit.Host/NUnitHostTestFrameworkProvider.cs`
- Modify: `source/DevTools.NUnit.Runtime/NUnitRuntimeSession.cs`
- Modify: `source/DevTools.NUnit.Runtime/DevTools.NUnit.Runtime.csproj`
- Delete superseded NUnit mechanism files only after parity tests pass.
- Modify NUnit Host/net48/runtime tests.

**Interfaces:**

- `NUnitGenerationPolicy : ITestingGenerationPolicy` owns NUnit runtime source,
  `nunit.framework.dll`, 4.6.1 validation, dependency collision preference,
  and NUnit-specific shared/private file policy.
- NUnit assembly-resolution policy owns host-shared `nunit.framework` and the
  NUnit contract/runtime activation rules.
- `NUnitRuntimeSession` implements `ITestingRuntimeSession`; any legacy
  discovery surface remains behind an NUnit-only compatibility interface.

- [ ] Write failing parity tests showing NUnit policy produces the same
  generation contents, version rejection, dependency choice, and host-shared
  framework behavior as before.
- [ ] Write a failing dependency test showing NUnit Runtime implements the
  neutral contract without exposing NUnit types through that interface.
- [ ] Run failures before changing production code.
- [ ] Implement NUnit generation, resolution, and runtime activation policies.
- [ ] Replace `NUnitRuntimeManager` mechanism with the common manager plus
  NUnit adapters; preserve error and diagnostic mapping.
- [ ] Delete duplicate NUnit hash/path/snapshot/index/session mechanism files.
- [ ] Run NUnit Host, NUnit Host net48, NUnit Runtime, Testing Host, and
  generation-isolation suites.
- [ ] Compile NUnit Host and Runtime for net48/net8/net10.

### Task 5: Split TestRunner Core And NUnit Command Provider

**Files:**

- Create: `source/DevTools.TestRunner.Core/DevTools.TestRunner.Core.csproj`
- Move framework-neutral parsing/host launch/debug/process infrastructure from
  `source/DevTools.TestRunner/` into the Core project.
- Create: `source/DevTools.NUnit.Runner/DevTools.NUnit.Runner.csproj`
- Move NUnit discovery/filter/legacy stdout/legacy pipe client and NUnit command
  implementation into the NUnit Runner project.
- Modify: `source/DevTools.TestRunner/DevTools.TestRunner.csproj`
- Modify: `source/DevTools.TestRunner/Program.cs`
- Add both projects to `RevitDevTool.slnx` and relevant packaging modules.
- Split/update TestRunner tests by ownership.

**Interfaces:**

- `DevTools.TestRunner.Core` exposes generic host execution and command context
  without any NUnit reference or literal.
- `DevTools.NUnit.Runner` exposes a registration entry point consumed by the
  executable composition root.
- `DevTools.TestRunner.exe` name, CLI entry point, host launch/debug behavior,
  and generic `--framework nunit` execution remain compatible.
- Local NUnit discovery remains entirely host-free.

- [ ] Write failing architecture tests for dependency direction and forbidden
  NUnit symbols in TestRunner Core.
- [ ] Write failing integration tests that compose the NUnit module and retain
  generic and legacy CLI/stdout behavior.
- [ ] Verify both tests fail before project extraction.
- [ ] Create the two libraries and move code according to ownership.
- [ ] Update composition, project references, solution, packaging, and tests.
- [ ] Prove `discover` does not instantiate host launch/pipe services.
- [ ] Run TestRunner Core, NUnit Runner, existing TestRunner, MTP, and adapter
  focused tests; compile the executable.

### Task 6: Decompose And Remove DevTools.NUnit.Core

**Files:**

- Move remaining generic files into `DevTools.Testing.Abstractions` or
  `DevTools.Testing.Transport`.
- Move NUnit discovery/filter/mapping files into NUnit MTP/Runner ownership.
- Move legacy DTO/protocol/compatibility files into
  `DevTools.NUnit.Transport`.
- Modify MTP, VSTest adapter, Runner, Host, Runtime, tests, solution, build, and
  packaging references.
- Delete: `source/DevTools.NUnit.Core/` after consumers are migrated.

**Required end state:**

- no `<Compile Include="..\DevTools.NUnit.Core\...">` source links;
- no `ProjectReference` to `DevTools.NUnit.Core`;
- no `DevTools.NUnit.Core.dll` in host packaging or shared-assembly policy;
- no source or solution entry for the project;
- generic projects contain no NUnit symbol or literal;
- legacy and generic behavior tests remain green.

- [ ] Add a failing repository architecture test that detects project
  references, source links, packaging entries, or tracked files for
  `DevTools.NUnit.Core`.
- [ ] Run it and confirm failure against the existing project.
- [ ] Move each remaining type to its decided owner and update namespaces.
- [ ] Replace linked compilation with project references or provider-owned
  source compiled exactly once per package.
- [ ] Delete the Core project and remove all solution/build/package entries.
- [ ] Run the architecture test and all focused core/NUnit/Runner/MTP/adapter
  tests.
- [ ] Build every touched multi-target project and pack NUnit MTP locally
  without publishing.
- [ ] Verify `git diff` contains no Polyfill, PolySharp, or ILRepack policy
  change.

## Risks And Recovery

- **Capability loss:** contract golden tests compare every legacy NUnit field to
  the neutral representation before legacy removal.
- **ALC identity break:** Abstractions remains loose and provider runtime
  implements only types from that assembly.
- **net48 binding regression:** retain old mechanism until NUnit policies pass
  net48 generation tests; delete duplicates only in Task 4.
- **Runner packaging regression:** keep the executable project as composition
  root and verify deployed/packed paths before deleting references.
- **Large pending diff:** no commits are made; each agent writes a report under
  the git-ignored SDD workspace and reviewers inspect task-scoped file lists.
- **Recovery:** restore only the files listed by the failed task from the
  pre-task patch/report; do not reset unrelated user changes.

## Progress

- [x] Independent boundary and polyfill-noise review complete.
- [x] Decision 0021 recorded.
- [x] Detailed implementation plan recorded.
- [x] Task 1: neutral contracts and capability parity.
- [x] Task 2: NUnit compatibility leaves generic dispatch.
- [x] Task 3: common generation/runtime kernel.
- [x] Task 4: NUnit policy migration and parity.
- [x] Task 5: TestRunner core/provider split.
- [x] Task 6: remove DevTools.NUnit.Core and source links.
- [x] Whole-change review and verification.
- [ ] User review of items 1-6.
- [ ] Separate PolySharp spike plan after approval.

## Decisions

- 2026-08-17: Items 1-6 are priority and execute sequentially because each
  changes the dependency graph consumed by the next.
- 2026-08-17: PolySharp is excluded from this plan and cannot begin before user
  review of items 1-6.
- 2026-08-17: No commits are created until explicitly authorized by the user.

## Validation

- Focused proof: Abstractions, Testing Host/Transport/MTP, NUnit Host/net48,
  NUnit Runtime, NUnit MTP, NUnit TestAdapter, Runner Core/NUnit Runner/Runner.
- Integration proof: local discovery host-free; generic and legacy execution
  mapping; two-generation isolation and cancellation.
- Build proof: touched shared projects for net48/net8/net10; local NUnit MTP
  pack without publish.
- Architecture proof: forbidden dependency/literal/source-link/package scans.

## Result

Items 1-6 are implemented and independently reviewed. Focused contract,
kernel, provider, Runner, MTP, adapter, package-consumer, and host builds were
verified. The NUnit Host suite retains one pre-existing spike fixture mismatch
(`spike-output-marker` versus the expected `spike-trace-marker`); no new host
test failure was introduced. PolySharp remains deferred.
