# NUnit Boundary Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. This plan is executed inline; do not spawn subagents.

**Goal:** Make NUnit projects contain only framework-specific discovery, host policy, and runtime behavior while shared host mechanics live under `DevTools.Testing.*`.

**Architecture:** `DevTools.Testing.Host` owns neutral request marshaling, assembly preflight, generation publication, and runtime-session lifecycle. The NUnit discovery assembly owns only local NUnit metadata/filter semantics; `DevTools.NUnit.Host` supplies NUnit generation and isolation policies; `DevTools.NUnit.Runtime` remains the direct NUnit API adapter.

**Tech Stack:** C#, .NET Framework 4.8, .NET 8/10 Windows, Microsoft Testing Platform, NUnit 4, xUnit v3 test harness.

**Spec:** `docs/decisions/0021-testing-kernel-and-provider-owned-framework-runtime.md` and the approved boundary review in the current task.

## Global Constraints

- Preserve local, host-free MTP discovery.
- Preserve Dynamo/NUnit framework sharing and net48 default-AppDomain
  `ScopedNetFramework` isolation (shadow `LoadFile`, no child AppDomain).
- Do not introduce a framework dependency from any `DevTools.Testing.*` project.
- Do not modify the existing pending changes in `DevTools.NUnit.Runner/Commands/NUnitRunnerCommands.cs` or `RunCommand.cs` except where a namespace/type migration requires it.
- Do not commit unless the user requests it.

---

### Task 1: Neutral host entry boundary

**Files:**
- Create: `source/DevTools.Testing.Host/TestingAssemblyPreflight.cs`
- Create: `source/DevTools.Testing.Host/MarshaledTestingRequestHandler.cs`
- Create: `source/DevTools.Testing.Host/TestingHostingExtensions.cs`
- Modify: `source/DevTools.Testing.Host/DevTools.Testing.Host.csproj`
- Modify: `source/DevTools.NUnit.Host/NUnitHostingExtensions.cs`
- Delete: `source/DevTools.NUnit.Host/NUnitAssemblyLoader.cs`
- Delete: `source/DevTools.NUnit.Host/MarshaledTestingRequestHandler.cs`
- Test: `tests/DevTools.Testing.Host.Tests/TestingAssemblyPreflightTests.cs`
- Test: `tests/DevTools.Testing.Host.Tests/MarshaledTestingRequestHandlerTests.cs`

**Interfaces:**
- Produces: `TestingAssemblyPreflight.ResolveAndEnsureLoadable(string)` and `IServiceCollection.AddGenericTestingHostServices()`.
- Consumes: `TestingProviderRegistry`, `IHostContextExecutor`, `IHostAppInfo`.

- [x] Add focused tests that require neutral preflight and marshaled host-context execution.
- [x] Run the Testing.Host suite and confirm RED because the neutral types do not exist.
- [x] Move the implementation and DI registration without retaining NUnit aliases.
- [x] Run Testing.Host and Execution registration tests GREEN.

### Task 2: Neutral generation manifest only

**Files:**
- Modify: `source/DevTools.NUnit.Host/Loading/NUnitGenerationPolicy.cs`
- Modify: `source/DevTools.NUnit.Host/Loading/NUnitIsolationPlan.cs`
- Modify: `source/DevTools.NUnit.Host/Loading/NUnitRuntimeSessionFactory.cs`
- Modify: `source/DevTools.NUnit.Host/NUnitHostingExtensions.cs`
- Delete: `source/DevTools.NUnit.Host/Loading/NUnitGenerationManifest.cs`
- Delete: `source/DevTools.NUnit.Host/Loading/NUnitGenerationManifestAdapter.cs`
- Delete: `source/DevTools.NUnit.Host/Loading/NUnitGenerationBuilder.cs`
- Delete: `source/DevTools.NUnit.Host/Loading/NUnitGenerationContentHash.cs`
- Test: existing NUnit Host and net48 generation suites, migrated to `TestingGenerationManifest`/`TestingGenerationStore`.

**Interfaces:**
- Consumes: `TestingGenerationStore`, `NUnitGenerationPolicy`, `TestingGenerationManifest`.
- Produces: one `NUnitRuntimeSessionFactory` over the neutral manifest (`Collectible` on modern TFMs, `ScopedNetFramework` on net48).

- [x] Add an architecture assertion rejecting the NUnit manifest/builder compatibility types and run RED.
- [x] Register `TestingGenerationStore` and `NUnitGenerationPolicy` directly.
- [x] Remove adapters/facades and migrate production/tests to the neutral manifest.
- [x] Run modern and net48 NUnit Host suites GREEN.

### Task 3: Narrow Provider to discovery ownership

**Files:**
- Rename: `source/DevTools.NUnit.Provider/` to `source/DevTools.NUnit.Discovery/`.
- Modify: MTP, Runner, tests, solution, and package project references/namespaces.
- Move: NUnit runner-path/environment configuration to `DevTools.NUnit.Mtp`.
- Delete: duplicate `HostRunOptions`; use `TestingHostOptions` directly.
- Rename: `RunnerTestFilter` to `NUnitDiscoveryFilter`.
- Rename: `NUnitTestingMapping` to a selection-only `NUnitSelectionMapping`.

**Interfaces:**
- Produces: local `NUnitMetadataDiscoverer`, `NUnitDiscoveredTest`, `NUnitDiscoveryFilter`, and NUnit selection mapping.
- Consumes: neutral `TestingSelection`; contains no host launch, process, IPC, runtime, or isolation behavior.

- [x] Add architecture tests for the discovery-only dependency boundary and run RED.
- [x] Rename the project and narrow its API.
- [x] Update MTP/Runner consumers and package closure assertions.
- [x] Run NUnit MTP, Runner, and TestRunner suites GREEN.

### Task 4: Remove remaining generic/dead NUnit Host helpers

**Files:**
- Move or delete: `NUnitGenerationPaths`, generic unload diagnostic wrappers, unused load exceptions.
- Modify: NUnit generation policy/copy planner and isolation session handle.
- Test: Testing.Host generation tests and NUnit isolation/unload tests.

**Interfaces:**
- Consumes: generic generation path/hash behavior and `AssemblyUnloadResult`.
- Produces: NUnit Host code containing only NUnit framework/version/closure/isolation decisions.

- [x] Add repository boundary assertions for forbidden generic compatibility helpers and run RED.
- [x] Delete dead code and use neutral/kernel result types directly where behavior is shared.
- [x] Run focused host/isolation tests GREEN.

### Task 5: Documentation and verification

**Files:**
- Modify: `docs/product/nunit-host-testing.md` only.
- Move completed plan to `docs/plans/completed/2026-08-18-nunit-boundary-cleanup.md` after proof.

- [x] Update the ownership table and remove stale VSTest/provider wording.
- [x] Build `DevTools.Testing.Host`, `DevTools.NUnit.Discovery`, `DevTools.NUnit.Host`, `DevTools.NUnit.Runtime`, and `DevTools.NUnit.Mtp` for their configured TFMs.
- [x] Run Testing.Host, NUnit Host modern/net48, NUnit Runtime, NUnit MTP, NUnit Runner, TestRunner, and Execution focused suites.
- [x] Run architecture scans and `git diff --check`; report any skipped live-host proof explicitly.

## Result

- Neutral host marshaling and assembly preflight now live in `DevTools.Testing.Host`.
- NUnit Host consumes `TestingGenerationManifest` directly; compatibility manifests, builders, duplicate hashing, paths, and unload wrappers were removed.
- `DevTools.NUnit.Provider` became the discovery-only `DevTools.NUnit.Discovery`; MTP uses `TestingHostOptions` directly.
- NUnit Runtime behavior, Dynamo-safe framework sharing, modern collectible isolation, and net48 default-AppDomain `ScopedNetFramework` isolation remain provider-owned.
- Verification covered focused tests, clean package consumption, a fresh ILRepack host output, and live `Arithmetic_runs_inside_host` on Revit 2024 (net48) and Revit 2025 (net8).
