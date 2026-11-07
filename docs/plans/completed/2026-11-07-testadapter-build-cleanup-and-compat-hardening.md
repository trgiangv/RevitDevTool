# Execution Plan: TestAdapter Build Cleanup And Compat Hardening

Date: 2026-11-07

## Status

Completed 2026-11-08 (compile, unit, matrix, and live proof on Revit 2024, 2025, 2027).
Follows the review of [0060](../../decisions/0060-framework-runtime-shims-ship-with-nettest.md)
after its plan [completed](2026-11-06-framework-shims-nettest-0060.md). The adapter
version bump and release are plan 0059 (P3b).

## Outcome

1. The adapter package build (`build/RevitDevTool.TestAdapter.props|targets`, the
   adapter csproj, `PackTestAdapterModule`) has no target that repeats an SDK default,
   no inline Roslyn task, no override of a private Microsoft.Testing.Platform property,
   and one list of the three frameworks for packing.
2. Host options reach the adapter as assembly metadata written from the csproj. A
   user-authored `testconfig.json` `devtools` key still wins. No merge step exists.
3. The framework floor in the runtime manifest is the package version the runtime was
   built against. The host compares it with the product version of the user's framework
   assembly, so build check and host check read the same number, and a prerelease sorts
   below its release.
4. A TUnit engine bind failure is read from the node's error property, not its name.
5. The remaining stops are one sentence: a missing framework assembly beside the test
   assembly, and a framework that did not come from a package gets a build warning.
6. The add-in layout assertion covers all three runtimes and their frameworks.
7. `PublicAPI.Shipped.txt` means the surface of the last stable `addin` release, the
   release runbook moves `PublicAPI.Unshipped.txt` into it, and no CI step beyond the
   analyzer exists (Phase C, replaces the diff step Phase B added).

## Context

- Decisions: 0060 (framework runtimes in nettest; §3 manifest `version`, §4 stops,
  §8 `Testing.Abstractions` add-only), 0024 (testconfig keys; amended here),
  0042 (seams that stay), 0055 (ranges), 0059 (trains).
- Measured 2026-11-07 on the sample outputs (net48 and net10): `nunit.framework`,
  `TUnit.Core`, `TUnit.Engine`, `MSTest.TestFramework`, and `Microsoft.Testing.Platform`
  all carry `ProductVersion` = package version + `+sha`. MSTest and MTP file versions
  are `x.500.yyy.z`, so the file version is not usable; the product version is.
- SDK defaults that make `DevToolsNetFxBindingRedirects` redundant:
  `AutoGenerateBindingRedirects=true` (`Microsoft.NET.Sdk.BeforeCommon.targets:103`),
  `GenerateBindingRedirectsOutputType=true` for Exe
  (`Microsoft.Common.CurrentVersion.targets:362`), `AutoUnifyAssemblyReferences=true`
  (`:368`). The package props already force `OutputType=Exe`.
- MTP 2.5.2 `Microsoft.Testing.Platform.MSBuild.targets` has no public hook for a custom
  `testconfig.json` section: `TestingPlatformCommandLineOptionDefault` fills
  `commandLineOptionDefaults` only, and `_TestingPlatformConfigurationFileSourcePath` is
  private. Assembly metadata is the public SDK path.
- TUnit main (`src/TUnit.Engine/Framework/TUnitTestFramework.cs`,
  `ReportUnhandledException`) publishes the unhandled node with
  `ErrorTestNodeStateProperty(exception)`; `FlattenedException` keeps the original in
  public `TUnitFailedException.WrappedException` and leaves `InnerException` null.
- Proof: `.agents/skills/build/SKILL.md`, `docs/agents/host-testing.md`,
  `tests/DevTools.TestAdapter.Tests` (`PackageConsumerTests` packs into an isolated
  feed), `tests/DevTools.Testing.Host.Tests`, `tests/DevTools.TUnit.Runtime.Tests`.

## Scope

In scope:

- Phase A (`nettest` only): adapter targets, props, csproj pack targets, pack module,
  `TestRunSettingsLoader`, the adapter tests and docs that describe them.
- Phase B (`addin` + `nettest`): manifest `version`, host checks, bind-failure reading,
  missing-framework sentence, build warning, layout assertion, CI diff check, ADR 0060
  amendment.

Out of scope:

- The 0042 seams, `NetFxModuleInitializer`, `WriteDiscoveryRefs`, the builder-hook
  removes, `TestingPlatformCommandLineArguments`.
- Moving `TestingRuntime.props` into C# constants.
- The adapter version bump: a release step of plan 0059.
- The net48 open items of the 0060 plan (second generation's
  `System.Threading.Tasks.Extensions`, retired resolver in the chain, `generation.retained`).

## Approach

### Phase A: adapter build cleanup

1. Delete `DevToolsNetFxBindingRedirects`. Document the one case it covered: a consumer
   that sets `AutoGenerateBindingRedirects=false` on net48 loses the MTP redirects.
2. Replace `GenerateTestConfig`, `MergeTestConfig`, `UpToDateCheckBuilt`, and the
   `_TestingPlatformConfigurationFileSourcePath` override with `AssemblyMetadata` items
   `devtools.<key>` for the seven `TestConfig.Keys`. `TestRunSettingsLoader` reads, in
   order: environment variable, MTP `IConfiguration` `devtools:<key>` (a user-authored
   `testconfig.json`), then the entry assembly's `AssemblyMetadataAttribute`. The
   `HostName` / `HostVersion` build errors move to the project check target.
3. Collapse the runtime folder choice to three rules (net48, .NET 10 or newer, .NET 8 or
   newer), drop the `_SelectedMtpHintPath` / `_AbstractionsHintPath` override seams, and
   keep one copy of each error.
4. Csproj: one `TestFramework` item list drives the sibling and runtime rows; the pack
   always restores and builds the six projects; `PackTestAdapterModule` restores the
   adapter and packs. `PackageConsumerTests.PackAdapter` only packs.
5. Docs: `docs/architecture/Testing/README.md` (consumer build order, bootstrap table,
   user-authored testconfig), `docs/product/host-testing.md`, `docs/agents/host-testing.md`,
   `docs/product/tunit-host-testing.md`, `.agents/skills/revit-test/references/project-setup.md`,
   and an amendment note in 0024.

### Phase B: compat hardening

1. `props/TestingRuntime.targets` writes `version` from the framework reference's
   `NuGetPackageVersion` (`@(ReferencePath)` by file name), falling back to the assembly
   version. `DevTools.Testing.Transport` gains `FrameworkVersion`: numeric part padded to
   four components, optional prerelease label that sorts below the release, `+build`
   ignored, display without a trailing `.0` revision. `RuntimeManifest.IsComplete` and
   `RuntimeCompatChecker` use it; the host reads `ProductVersion` of the user's framework
   assembly and falls back to the assembly version. `RuntimeBindFailure` takes the same
   type. The build check already compares package versions; its floor is now the same
   string. Compatibility: an older add-in parses `5.0.0` with `Version.Parse` and
   compares `5.0.0.0 < 5.0.0` as false; a newer add-in pads an old `5.0.0.0` manifest.
2. `CheckFrameworkVersion` warns when the framework is not resolved from its package.
3. `TUnitEngineBindFailure.TryRead(TestNode)` reads `ErrorTestNodeStateProperty.Exception`,
   unwraps `TUnitFailedException.WrappedException`, keeps the §4 type list and the
   `TUnit.` / `Microsoft.Testing.` message filter, and rethrows the original.
4. A missing framework assembly beside the test assembly is a
   `TestingRuntimeIncompatibleException` with one sentence.
5. `props/ILRepack.targets` (`AssertHostLayout`) also fails on `DevTools.TUnit.Runtime.dll`,
   `DevTools.MSTest.Runtime.dll`, `TUnit.Core.dll`, `TUnit.Engine.dll`,
   `MSTest.TestFramework.dll`, and `Microsoft.Testing.Platform.dll`.
6. `Compile.yml` fails a pull request whose diff removes a line from
   `source/DevTools.Testing.Abstractions/PublicAPI.Shipped.txt`.
7. ADR 0060 §3 and the architecture README describe `version` as the package version.

### Phase C: PublicAPI semantics (2026-11-07 review of Phase B6)

Finding: `PublicAPI.Shipped.txt` was generated from the working tree at the end of
plan 0060, not from a release (adapter `0.2.0` on nuget.org lacks `TestingProcessHold`
and `TestSelectionMatcher`, which the file lists). No release step moves `Unshipped`
into `Shipped`. The B6 CI diff step fails the one legitimate removal flow, a
`*REMOVED*` line in `Unshipped` resolved at a major release, so it needs a bypass for
its only valid case. The analyzer catches accidental surface changes; it does not
know which change is breaking (a new member on an existing interface is one line
added and breaks every shipped runtime on net48).

1. Delete the `Check shipped public API` step from `.github/workflows/Compile.yml`.
   Nothing else in the repo references it.
2. ADR 0059 §6 (prerelease flow) gains the release step: a stable `addin` release
   moves every line of `PublicAPI.Unshipped.txt` into `PublicAPI.Shipped.txt` and
   leaves `Unshipped` empty; a `*REMOVED*` line is resolved there and is allowed
   only when `MAJOR` rises (0055 §3).
3. ADR 0060 §8, analyzer bullet, rewritten: `Shipped` is the surface of the last
   stable `addin` release, today the 0060 baseline until the first stable release;
   new symbols go to `Unshipped`; a removal is a `*REMOVED*` line and a new major;
   the analyzer turns every surface change into a declared text diff and does not
   decide what is breaking, so adding a member to an existing interface, changing
   the `entry` constructor, and changing `AssemblyVersion` are review rules.
4. `docs/architecture/Testing/README.md` sentence on the two files says the same in
   one line and links 0060 §8.

Expectations: no code change; `Compile.yml` still valid YAML; the three docs say
one truth each; this plan's Progress B6 is marked replaced by C1.

## Risks And Recovery

- A consumer that sets `AutoGenerateBindingRedirects=false` on net48: documented, not
  guarded. Recovery: set it back or remove the override.
- Assembly metadata is read from `Assembly.GetEntryAssembly()`. A host that runs the
  adapter without an entry assembly falls back to `IConfiguration` and the environment,
  as today. Recovery: author `testconfig.json`.
- A framework assembly without `AssemblyInformationalVersion` falls back to the assembly
  version, which is the rule before this plan.
- A prerelease framework passes the build check (`VersionLessThan` ignores the label)
  and stops at the host with the below-floor sentence. Documented.
- `TUnitFailedException.WrappedException` is public API of TUnit.Engine; if a TUnit
  release changes it, the bind failure reads as a test result again, as before this plan.
- Rollback: each phase is one commit group; reverting Phase B keeps Phase A valid.

## Progress

- [x] A1 binding redirects target removed
- [x] A2 assembly metadata replaces testconfig generation
- [x] A3 runtime folder and error dedupe (`CheckTestProject`)
- [x] A4 csproj pack targets and module (`_PackProject` metadata is `PackOutput`; `Output` is reserved, MSB4118)
- [x] A5 docs
- [x] B1 package version floor (manifests read `5.0.0`, `1.78.0`, `4.5.2` on all three TFMs)
- [x] B2 build warning (no in-repo consumer resolves a framework outside a package, so the text is unverified)
- [x] B3 TUnit bind failure from the node property
- [x] B4 missing framework sentence
- [x] B5 layout assertion (XML checked; runs only in an ILRepack host build)
- [x] B6 CI diff check (filter run locally against origin/main and a sample diff; replaced by C1 the same day)
- [x] B7 ADR and architecture docs
- [x] C1 Compile.yml diff step removed (no other reference in the repo)
- [x] C2 0059 §6 release step for Unshipped → Shipped (paragraph after step 5; applies to `addin` only)
- [x] C3 0060 §8 analyzer bullet rewritten
- [x] C4 architecture README sentence

## Decisions

- 2026-11-07 (owner): proceed with the review's proposals A1 to A7 and B1 to B4; the
  `TestingRuntime.props` to C# move stays optional and is not done here.
- 2026-11-07: `TUnitEngineHost` rethrows the bind failure through
  `ExceptionDispatchInfo` so the original stack reaches the host log.
- 2026-11-07: `FrameworkVersion` stores the numbers padded, so `5.0.0.0` and `5.0.0`
  are equal; test fixtures that need a product version are compiled with Win32 version
  resources because `FileVersionInfo` reads the resource, not the attribute.
- 2026-11-07 (owner): the B6 CI diff step is over-engineering for this repo; keep the
  analyzer and the two files, give `Shipped` the release meaning, and drop the step.
  The TestRunner move into the nupkg (0059 §3) runs in parallel under plan 0059 P3a.
- 2026-11-07: host options go through assembly metadata, not a second config file,
  because MTP 2.5.2 exposes no public hook for a custom `testconfig.json` section and the
  SDK hashes `AssemblyMetadata` items for incremental builds.

## Validation

- Focused proof: `dotnet run --project tests/DevTools.TestAdapter.Tests/...`
  (architecture, framework map, loader, package consumer),
  `tests/DevTools.Testing.Host.Tests`, `tests/DevTools.Testing.Transport.Tests`,
  `tests/DevTools.TUnit.Runtime.Tests`, `tests/DevTools.TestRunner.Tests`.
- Integration proof: `scripts/test-adapter-matrix.ps1`, then one live run per framework
  on a Revit year per TFM with the deployed add-in.
- Repository-required checks: compile per `.agents/skills/build/SKILL.md` for the touched
  projects.

## Result

2026-11-07, compile and unit proof on the working tree (no live host, no
`scripts/test-adapter-matrix.ps1` yet):

| Project | Passed / total |
|---|---|
| DevTools.TestAdapter.Tests (includes `PackageConsumerTests`, isolated pack on net48, net8, net10) | 90 / 90 |
| DevTools.Testing.Transport.Tests | 65 / 65 |
| DevTools.Testing.Host.Tests | 100 / 100 |
| DevTools.TUnit.Runtime.Tests | 19 / 19 |
| DevTools.NUnit.Host.Tests | 27 / 27 |
| DevTools.TestRunner.Tests | 33 / 33 |

Packed manifests read `5.0.0`, `1.78.0`, and `4.5.2` on all three TFMs. The adapter
csproj builds on Debug with no warnings.

2026-11-08, live proof with the deployed add-in (Release host build) and the packed
adapter restored by the samples:

- `scripts/build-host.ps1 -HostApp Revit -Year 2024,2025,2027`: `AssertHostLayout` ran
  after `RepackAddinFiles` on all three years (`-v:d` log); the deployed payloads hold
  no runtime or framework dll and keep `DevTools.Testing.Abstractions.dll` loose.
- `scripts/test-adapter-matrix.ps1` with the MSTest sample added: 15 of 15 rows `ok`
  (NUnit 71, TUnit 62, MSTest 37, Civil3D 3 and 2 tests listed per year).
- `dotnet test` per framework on one long-lived Revit per year, `Debug.Autodesk.<year>`:

| Year (TFM) | NUnit | TUnit | MSTest |
|---|---|---|---|
| 2024 (net48) | 71 pass, 2 demo fail, 2 skip | 59 pass, 1 demo fail, 1 skip | 35 pass, 2 skip |
| 2025 (net8) | 71 / 2 / 2 | 59 / 1 / 1 | 35 / 2 |
| 2027 (net10) | 71 / 2 / 2 | 59 / 1 / 1 | 35 / 2 |

  The failures are the samples' intentional ones. One PID per year served all three
  frameworks; no `crash_*` log. `devtools.runnerPath` metadata on all nine
  project-year pairs names the runner inside the restored package.
- Negative (the variable was removed the next day; the package runner is the only
  one): `DEVTOOLS_TESTING_RUNNER_PATH` set to a missing file stopped in 0.2 s with
  one sentence naming the path and the variable; the stale runner in
  `%LOCALAPPDATA%\RevitDevTool\current` was not used.

Limitations:

- The CI public-API step was removed by Phase C; the analyzer in the build is the only
  mechanical check.
- The framework-not-from-package warning has no in-repo consumer that triggers it.
- `AssertHostLayout` checks `$(TargetDir)`, not the deployed bundle; the bundle scan
  above was a separate step.
- The negative sentence blamed the package even when the variable was the cause;
  resolved 2026-11-08 by removing every runner-path override (plan 0059, Open after P3a).
- Phase C landed with no code change; the TestRunner move ran in parallel as plan
  0059 P3a (its result table is there), so the first `nettest` release carries both.
