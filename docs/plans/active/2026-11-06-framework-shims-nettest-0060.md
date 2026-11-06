# Execution Plan: Framework Runtimes Ship With NetTest (ADR 0060)

Date: 2026-11-06

## Status

Active. P0, P1, P2 (MSTest), and P3 (TUnit) done; NUnit stays on the old path. Decision: [0060](../../decisions/0060-framework-runtime-shims-ship-with-nettest.md)
(Accepted). Depends on the `nettest` train of
[2026-11-06-release-trains-0059](2026-11-06-release-trains-0059.md) for shipping;
the code work here can start before that plan's P3.

Visual of the agreed design (owner's private artifact):
https://claude.ai/artifact/Snj9TnxPnrRfTwKTTnrVF2

## Outcome

1. The add-in ships no framework runtime: no `NUnitRuntime\`, `TUnitRuntime\`,
   `MSTestRuntime\`, no framework assembly, no framework version pin.
2. The framework runtimes `DevTools.{NUnit,TUnit,MSTest}.Runtime.dll` and a manifest each ship in
   the `RevitDevTool.TestAdapter` nupkg and land in the user's output.
3. Inside the host, the user's framework copy is always used; the framework must
   have the same major as the baseline and be no lower. A framework update with no
   break, internals included, needs no add-in or adapter update.
4. The add-in and the adapter never have to update together inside an add-in major;
   they meet only through the `testing.runtime` range.
5. Every mismatch ends with one sentence that names the fix, before tests run or as
   soon as a bind fails.

## Context

- Decision: 0060 (this plan implements it). Related: 0021 (kernel and
  provider-owned runtime, partly replaced), 0038 and 0042 (pins, replaced), 0055
  (ranges, `ProtocolRange`/`ProtocolSender` in `DevTools.Ipc`), 0017 (output
  routing, unchanged), 0019 and 0023 (ILRepack and isolation kernel).
- Where the lock is today (agent report 2026-11-06; line numbers to re-check):
  - Equality pins: `source/DevTools.Testing.Host/Loading/TestingGenerationPins.cs:35`
    (`if (version != expected)`, verified) and `:51`; called from
    `source/DevTools.Testing.Host/Loading/TestingGenerationSpec.cs:38-41`
    (`ValidateConsumerPins`).
  - Pin values: `source/DevTools.Testing.Host/NUnit/NUnitGenerationPolicy.cs:15`
    (`ExpectedNUnitFileVersion = "5.0.0.0"`, verified),
    `source/DevTools.Testing.Host/TUnit/TUnitGenerationPolicy.cs:16-27`,
    `source/DevTools.Testing.Host/MSTest/MSTestGenerationPolicy.cs:18-31`,
    build-time `<Error>` in `source/DevTools.MSTest.Runtime/build/MSTestRuntimePayload.targets:26-33`.
  - Payloads in the add-in: `source/RevitDevTool/RevitDevTool.csproj:66-68` and
    `source/DevTools.{NUnit,TUnit,MSTest}.Runtime/build/*RuntimePayload.targets`;
    ILRepack exclusions at `RevitDevTool.csproj:17-18`; Polyfill note at `:25`.
  - Runtime source and activation: `source/DevTools.Testing.Host/Loading/RuntimeSource.cs:14-48`
    (`ResolveBeside(addin)`), `source/DevTools.Testing.Host/Runtime/IsolatedRuntimeActivator.cs:27-38`,
    `source/DevTools.Testing.Host/Runtime/ManifestRuntimeSessionFactory.cs:19`
    (only `Testing.Abstractions` shared). **A runtime "manifest" concept already
    exists here: extend it rather than adding a second one.**
  - Runtime copy overwrites the user's copy: `MergeRuntimeDependency` in
    `source/DevTools.Testing.Host/Loading/Generation/TestingGenerationFiles.cs:178-205`.
  - NUnit shared per process: `source/DevTools.Testing.Host/NUnit/NUnitFrameworkHostShare.cs:47-53, 77`.
  - Generic failure: `source/DevTools.Testing.Host/TestingRequestHandler.cs:201-205`
    (`ProviderFailed` with `ex.ToString()`, poisons the session).
  - `testing/hello` has no framework or runtime-contract data:
    `source/DevTools.Testing.Transport/TestingProtocol.cs:50-58`.
  - Cross-train ABI: `source/DevTools.Testing.Abstractions/Runtime/ITestingRuntimeSession.cs`.
  - Internals the framework runtimes use: `NUnit.Framework.Internal` / `Internal.Builders`
    (`source/DevTools.NUnit.Runtime/NUnitFilterFactory.cs`, `NUnitResultMapper.cs`,
    `NUnitAssemblyBuilder.cs`), private `TestContext.DefaultWorkDirectory`
    (`NUnitAssemblyBuilder.cs:107`, verified), `source/DevTools.TUnit.Runtime/TUnitEngineBindings.cs:102-119`,
    `source/DevTools.MSTest.MTP/MtpDiscoveryInternals.cs:25-56` (linked into the runtime).
  - The nupkg already ships testhost siblings `DevTools.{NUnit,TUnit,MSTest}.MTP.dll`
    under `build/runtime/{tfm}`; they share source and pins with the framework runtimes
    (`DevTools.NUnit.MTP.csproj:18-23`, `DevTools.TUnit.MTP.csproj:22-26`).
- Pins today: NUnit 5.0.0, TUnit 1.78.0, MTP 2.5.2 (`Directory.Packages.props`),
  MSTest.Sdk 4.5.2 (`global.json`).
- Proof: `.agents/skills/build/SKILL.md`, `.agents/skills/revit-test/SKILL.md`,
  `docs/agents/host-testing.md`, `docs/agents/test-matrix.md`,
  `docs/agents/mcp-integration-test.md`.

## Locked decisions (do not re-open without the owner)

| # | Decision | Why |
|---|----------|-----|
| S1 | Framework runtimes and their manifests move from the add-in to the `nettest` nupkg (`build/runtime/{tfm}`), copied to the user's output by `buildTransitive` targets. The add-in keeps only the kernel and `Testing.Abstractions`. | The coupling is framework runtime ↔ framework, both chosen by the user in one csproj; it does not belong in the add-in |
| S2 | The user's framework must have the **same major** as `version` and be **no lower** than `version`. The manifest has **one** framework field, `version`, the `AssemblyVersion` of the framework assembly the framework runtime was built against, read by the build (never by hand); it is the floor, its major is the supported major, and the full value appears in messages. | Owner: "giữ version 1 cái thôi tự suy ra major"; 2026-11-06: lower than the framework runtime's build version is not accepted |
| S3 | **No** `verified` list, **no** online compat index, **no** canary or golden behavior suite as a requirement. | Owner: over-engineering; online index needs GitHub release updates and a network check per run. If a rare minor breaks, the user steps back or updates the adapter |
| S4 | NUnit 5 is the only supported major. Do not design for NUnit 4. | Owner: default is already 5 |
| S5 | Host order: (1) manifest present → (2) `testing.runtime` ranges overlap → (3) framework major matches → (4) framework not lower than `version` → (5) load framework runtime + user closure (user copy always wins; testing sessions only may bind a newer file on .NET) → (6) run; a bind failure (ADR 0060 §4 list, unwrapped, naming a framework or platform assembly) becomes one sentence. Each stop is `ProtocolIncompatible` with the sentence, not `ProviderFailed`, and does not poison the session. The adapter has no check in the testhost; the runner checks `testing.runtime` on `testing/hello`. | Safe enough with no maintained lists. Owner removed the adapter check 2026-11-06 |
| S6 | Build check in the package's existing `build/` targets: fail when the resolved framework package has another major or a lower version than the manifest, and when `TestingFramework=tunit` has no `TUnit` package. Read `NuGetPackageVersion`; RAR `Version` is empty on .NET. Nothing else at build time. | Early, cheap, deterministic |
| S7 | NUnit stays host-shared, one copy per Revit process; another file version in the same process stops with "Revit has NUnit <loaded> loaded from an earlier run. Restart Revit to run tests with NUnit <requested>." | NUnit static state is per process; no two versions in one process |
| S8 | `Testing.Abstractions` and the `entry` constructor signatures change by addition only: new capability = new interface; the framework runtime keeps `entry` on the old interface and creates the capability in a separate type and non-inlined method after probing by name; the host detects it with `is`; no default interface members (net48); `AssemblyVersion` set to `1.0.0.0` in the project while `testing.runtime` `Min = 1`; the public surface is declared in `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` and checked on every build (`Microsoft.CodeAnalysis.PublicApiAnalyzers`, RS0016 and RS0017 fail the build); a removal that also deletes its line is caught in review, not by the tool. `Current` +1 for a capability; `Min` +1 only in a new add-in major. | The cross-train ABI; P0 measured the `TypeLoadException` and the version mismatch |
| S9 | Moving a framework pin is a normal `nettest` change, only when a framework runtime must change, because it raises the floor: keep CI green, bump the version, and write in the changelog the framework versions it was built against and any internal-API change. Never an `addin` release. | Owner: internal-flow changes are breaking logic to verify and note in the release, nothing heavier. Baseline = today's pins |
| S10 | No legacy (0059 L1): no fallback to framework runtimes beside the add-in. An adapter without a manifest gets "RevitDevTool.TestAdapter <v> is not compatible with RevitDevTool <v>. Update RevitDevTool.TestAdapter to the latest version." | Clean redesign |
| S11 | Order in the repo: MSTest → TUnit → NUnit. Users get all three in one `addin` major and one `nettest` release. | Smallest first; users change setup once |
| S12 | A framework runtime needs nothing outside the user's closure: in-box on .NET; ILRepack + internalize on net48 when the types stay inside the framework runtime (NUnit: `System.Reflection.Metadata`, `System.Collections.Immutable`, `System.Memory`, `System.Buffers`, `System.Numerics.Vectors`); lowest compiling reference when types cross to the framework (TUnit: `Microsoft.Bcl.AsyncInterfaces`). No NuGet dependencies for this. | Owner chose merge 2026-11-06; one package serves all three frameworks |

Rejected (with reason) — do not propose again:

- `verified` list or exact pins in the manifest: every framework patch would need a
  `nettest` release.
- Online verified-pairs index in the `channels` release: maintenance plus a network
  dependency per run (owner rejected).
- Framework runtimes on public framework API only (NUnit `FrameworkController`, MTP in-process):
  a rewrite larger than the risk.
- The add-in carrying one framework runtime per framework version.
- Keeping the pins with nicer errors.

## Exact sentences (contract; keep wording)

- No manifest: `RevitDevTool.TestAdapter <v> is not compatible with RevitDevTool <v>. Update RevitDevTool.TestAdapter to the latest version.`
- Range mismatch: the 0055 §4 sentence (`<A> is not compatible with <B>. <update message of the older side>`).
- Major mismatch: `<Framework> <found> is not supported by RevitDevTool.TestAdapter <v> (built for <Framework> <major>.x). Update RevitDevTool.TestAdapter to a version for <Framework> <found major>.`
- Below the floor: `<Framework> <found> is older than the <version> that RevitDevTool.TestAdapter <v> was built against. Update <Framework> to <version> or later.`
- No `TUnit` package: `RevitDevTool.TestAdapter with TestingFramework=tunit needs the TUnit package. Reference TUnit instead of TUnit.Core.`
- Bind failure: `<Framework> <found> changed an API that RevitDevTool.TestAdapter <v> uses (built against <version>). Update RevitDevTool.TestAdapter, or use <Framework> <version>.`
- Second NUnit: `Revit has NUnit <loaded> loaded from an earlier run. Restart Revit to run tests with NUnit <requested>.`
- Use the host app name instead of "Revit" for AutoCAD-family hosts.

## Manifest shape

```json
{
  "framework": "NUnit",
  "entry": "DevTools.NUnit.Runtime.NUnitRuntimeSession",
  "contract": { "current": 1, "min": 1 },
  "assembly": "nunit.framework",
  "version": "5.0.0.0"
}
```

No `netfx` section: `NetfxClosureBind` needs no framework knowledge, and the omit list
in `NUnitRuntimePayload.targets:19` has no job once the closure is the user's. Add it
back only if a real framework runtime run needs it.

## Scope

In scope: 0060 §1–§10.

Out of scope: release pipeline (plan 0059), changing 0017 output routing,
public-API rewrites of the framework runtimes, NUnit 4.

## Approach

### P0 Spikes (must pass before code)

Done outside a host; results under Decisions. MSTest and TUnit full session runs in
the user's closure are proved in P2 and P3.

### P1 Shared infrastructure

- Kernel: a testing-only rule on .NET that binds a newer file from the manifest and
  a request for a lower version to the user's copy, covering both
  `ManifestAssemblySource.Resolve` and `AssemblyIsolationContext.TryValidateCandidate`.
  C# script (`ScriptIsolationPlan.cs:32`) and MCP isolation stay as they are. Log a
  rejected candidate: today a version mismatch publishes no diagnostic.
- Manifest model and reader in `DevTools.Testing.Host` (extend the existing
  `ManifestRuntimeSessionFactory` concept); `testing.runtime` range via
  `DevTools.Ipc` `ProtocolRange`.
- Build: each `DevTools.*.Runtime.csproj` writes its manifest at build, `version`
  read from the built framework assembly (S2). No version literal in code or
  comments (owner rule). One MSTest pin source: `global.json`; remove the literal
  `PackageVersion` in `DevTools.MSTest.Runtime.csproj`.
- Nupkg: pack framework runtimes + manifests into `build/runtime/{tfm}`; the package's `build/`
  targets copy them to the output and run the checks (S6). Copy, do not `Reference`,
  so a framework runtime never enters the testhost's compile or `deps.json`.
- Host checks S5 1–4, bind-failure mapping (S5 step 6), `ProtocolIncompatible`
  without poisoning the session (`TestingRequestHandler.cs:201-205`).
- `testing/hello` carries the host `testing.runtime` range; the runner checks it
  against the manifest. A missing range reads as too old (0055 §4).
- PublicApiAnalyzers for `Testing.Abstractions`; set `AssemblyVersion` in the project (today
  `build/Modules/SolutionCompile.cs:27` stamps add-in copies `4.0.0.0` while the
  adapter's copy is `1.0.0.0`).
- Consumer hooks in `source/DevTools.TestAdapter/build/hooks/*.cs` use
  `ArgumentNullException` with no `using System`; they fail to compile without
  `ImplicitUsings`. Fix while in the package surface.

### P2 MSTest

- Framework runtime from the user's output; remove `MSTestGenerationPolicy` pins, the
  `MSTestRuntimePayload.targets` `<Error>`, and `MSTestPlatformPolicy.EnsurePinnedVersions`;
  stop shipping `MSTestRuntime\`.
- Remove `MergeRuntimeDependency` override behavior for MSTest (user copy wins).

### P3 TUnit

- Same as P2 for TUnit; TUnit.Engine from the user's output; lowest compiling
  `Microsoft.Bcl.AsyncInterfaces` reference (S12); `TUnit` package check (S6).

### P4 NUnit

- Same as P2 for NUnit; `Share` instead of `Pin`; shared-copy rule and sentence (S7);
  in-box and merged dependencies (S12).
- Run async tests on net48 with the merged framework runtime. If they fail, fix the failure (see
  Decisions).

### P5 Remove the old path

- Delete `TestingGenerationPins` equality checks, `RuntimeSource.ResolveBeside`,
  the imports at `RevitDevTool.csproj:66-68` and `ACadDevTool.csproj:63-65`, the
  `*HostPackaging.targets`, and the `*RuntimePayload.targets` that only served the
  add-in. Keep the layout assertions that still hold (`Testing.Abstractions` stays
  loose beside the add-in; host JSON/IPC stays merged), and drop the stale
  `DevTools.NUnit.Runtime.dll` entry at `RevitDevTool.csproj:17`.
- Docs, one layer each: `docs/product/host-testing.md`,
  `docs/product/tunit-host-testing.md`, `docs/architecture/` testing pages,
  `docs/agents/host-testing.md`, `docs/agents/test-matrix.md`,
  `.agents/skills/revit-test/SKILL.md` (pin statements).

### P6 Live verification

- Run every framework in Revit years covering net48, net8, and net10, plus AutoCAD
  (per `docs/agents/mcp-integration-test.md` / `host-testing.md`). Include: framework
  minor bump runs (TUnit above the baseline); below the floor → build error; major
  mismatch → build error; second NUnit version → restart sentence; adapter without
  manifest → sentence; source file and line present on NUnit results in net48 and net8.

## Risks And Recovery

- net48 binding fails in a real host: stop; ask the owner before any fallback (for
  example strong-naming or a host-side redirect).
- A framework runtime needs a host-only file that used to sit in the payload: apply S12, not back
  into the add-in.
- P0 proved load, JIT, and session runs outside a host. A framework behavior change
  that binds cleanly is not caught by any check here.
- Partial landing: frameworks move one at a time in the repo, but nothing ships until
  P6 passes for all three (S11). Until then the add-in keeps the old path for the
  frameworks not yet moved.

## Progress

- [x] P0 spikes (results in Decisions; no host)
- [x] Live check of the net48 NUnit async failure (Revit 2024): not reproduced today; recheck in P4 and P6
- [x] P1 shared infrastructure (machinery only; the current path still ships `*Runtime\`, keeps the equality pins, and no framework has moved)
- [x] P2 MSTest (the add-in ships no `MSTestRuntime\`; TUnit and NUnit stay on the old path)
- [x] P3 TUnit (the add-in ships no `TUnitRuntime\`; TUnit 1.81.0 ran on Revit 2024 and 2027 with no add-in or adapter change)
- [ ] P4 NUnit
- [ ] P5 remove old path + docs
- [ ] P6 live verification

## Decisions

- 2026-11-06: Design locked with the owner (table above). Earlier drafts with a
  `verified` list, a `supports.range`, an online index, and golden/canary suites were
  dropped as over-engineering.
- 2026-11-06 (owner): The goal is that a framework update with no breaking change
  needs no add-in or adapter update. "Breaking" includes the framework internals the
  add-in and adapter sides use, not only its public API. A framework version lower
  than the framework runtime's build version is not accepted (S2). Today's pins are the baseline.
- 2026-11-06 (owner): ADR accepted for implementation. Removed the adapter's testhost
  check (S5). Merge for net48 (S12). `TUnit` package required, as a plain constraint.
- 2026-11-06 P0 results (scratch harnesses, real kernel, no host):
  - Bind (fake framework, framework runtime built at 5.1, 509 rows): on .NET only the exact version
    passes today; a patched kernel passes newer and lower; CoreCLR accepts both from
    `ALC.Load`. net48 passes newer today. `Pin` rejects any difference; `Share` passes.
  - Abstractions: passes when both copies have the same version; on .NET, `Pin`
    rejects a mismatch either way. An `entry` type implementing an interface the host
    lacks throws `TypeLoadException`; a separate type in a non-inlined method after a
    probe passes.
  - Drift: TUnit 1.79–1.81 bind cleanly on a 1.78 framework runtime. Framework runtimes from older revisions load
    and JIT on newer closures. The MTP 2.4.0 constructor change is invisible to compile,
    static, and JIT probes, because it is reflective.
  - Real runs through the framework runtime session: with nothing added, NUnit loses source file and
    line on net48 and net8 with no message, and statuses of non-async tests stay
    correct; TUnit shows no difference on all TFMs. Merged NUnit framework runtime on net48 restores
    source location with the same non-async statuses and no type identity failure,
    376 KB to 1.4 MB. In-box `System.Reflection.Metadata` on net8 restores it too.
- 2026-11-06 Found, not caused by this work:
  - net48 NUnit async tests report Error with `FileNotFoundException` for
    `System.Threading.Tasks.Extensions`, varying per run, reproduced with today's
    factory in the harness. Likely cause from code: `nunit.framework` is pinned, so
    `NetfxAssemblyIsolationContext.ShouldServe` is false for its requests. Owner: fix in
    P4 if the live check confirms it.
    Live check 2026-11-06, Revit 2024, today's deployed add-in, `Async_delay_then_pass`
    in a fresh process: passed 6/6, so not confirmed. Not ruled out either: that session
    has the framework runtime's `System.Threading.Tasks.Extensions` 4.2.4.0 loaded from the shadow directory
    and other add-ins' copies in the domain (pyRevit, Carbon Insights), and the post-split
    framework runtime loads differently. P4 and P6 must run async tests on net48 with the merged framework runtime;
    fix it then if they fail.
  - `NUnitFilterFactory.RunNothingXml` ran all 11 tests in the harness, though it is
    documented as matching none. Owner: fix separately from this plan.
- 2026-11-06 P1 (verified by tests and a scratch consumer, no host):
  - Kernel: `ManifestAssemblySource(..., bindsAnyVersion: true)` is the testing-only rule,
    on .NET and net48. Nothing sets it yet. A version rejection now publishes
    `managed-candidate-rejected`; no testing plan has a diagnostic sink yet, so P2 sets one.
  - Not wired yet, for P2 to call: `RuntimeCompatChecker.Check` (stops 1 to 4),
    `RuntimeBindFailure.TryExplain` (stop 6), and `bindsAnyVersion`. The handler already maps
    `TestingRuntimeIncompatibleException` to `ProtocolIncompatible` without poisoning.
  - Live on the current path: the runner checks `testing_runtime` on `testing/hello` when
    the output has a manifest, so a host without the field gets the update sentence.
  - `RuntimeManifest` and its reader live in `DevTools.Testing.Transport`, not
    `DevTools.Testing.Host`, because the runner reads the manifest too and does not
    reference the host. The adapter `<v>` comes from the product version of
    `DevTools.TestAdapter.dll` in the output, so the manifest shape did not change.
  - `testing.runtime` has one source: `props/TestingRuntime.props`. It generates
    `TestingRuntimeContract` in Transport and feeds the manifest the build writes.
  - The current path now always takes the add-in's framework runtime over the copy the
    adapter puts in the user's output (`TestingGenerationSpec.AddRuntimeFile`).
  - API compatibility (owner, replaces the assembly-level check): no DLLs in git and a
    reviewable text diff, so `Testing.Abstractions` uses
    `Microsoft.CodeAnalysis.PublicApiAnalyzers` in every build. One `PublicAPI.Shipped.txt`
    and `PublicAPI.Unshipped.txt` serve all three TFMs because the surface is the same.
    `PublicAPI.Shipped.txt` was generated from the current surface, not from a released
    assembly, so it records no comparison with the last release. Temporarily adding a
    public member failed with RS0016 and renaming one with RS0016 and RS0017, on net48,
    net8, and net10. Known limit: deleting a line passes the check, so a removal is caught
    in review, not by the tool.
  - The adapter nupkg grew from 2.5 MB to 4.8 MB with framework runtimes and pdbs.
  - Messages drop a trailing `.0` fourth part (`1.78.0`, not `1.78.0.0`) and keep a non-zero
    one. The manifest keeps four parts, and comparison uses all four.
  - Found while proving the net48 rejection: MSTest's `AssemblyResolver` on .NET Framework
    keeps a per-process map from requested name to assembly and answers `AssemblyResolve` from
    it, so one test's bind can satisfy a later session's request for the same name. The test
    now uses a unique fixture name. P2 should check whether an in-host MSTest run has the
    same resolver, because it would hide a bind failure.
- 2026-11-06 P2 (verified live on Revit 2024, 2025, 2027 and Civil 3D 2026):
  - Shape for P3 and P4: `OutputGeneration` builds the plan from the user's output after
    `RuntimeCompatChecker.Check`, so a stop publishes nothing. `OutputSessionFactory` activates
    `manifest.Entry` with `bindsAnyVersion: true` and a logger sink. `IsolatedRuntimeActivator` and
    `IsolatedRuntimeSessionHandle` take an optional failure explainer that covers activation and
    run, and the old path passes none.
  - The sink logs selected candidates at Debug and rejected ones at Warning to the host log. A
    bind failure logs the original exception there, because the user sees only the sentence.
  - `DevTools.Testing.Host` no longer references `DevTools.MSTest.Runtime`, and
    `MSTestHostPackaging.targets`, `MSTestRuntimePayload.targets`, and `MSTestPlatformPolicy` are
    deleted. The pin in `TestingGenerationSpec`, `RuntimeSource.ResolveBeside`, and
    `MergeRuntimeDependency` stay for TUnit and NUnit until P5. No `Testing.Abstractions` member
    was added.
  - Samples, identical on all three years: 35 passed, 2 skipped (`Ignored_is_listed`,
    `Linux_only_is_listed_not_run`), 0 failed. The shadow copy's framework, adapter, MTP, runtime,
    and manifest match the user's output by hash, and it holds no `Testing.Abstractions`.
  - Each stop was proved live on Revit 2024 by editing the sample output, with a full run passing
    right after in the same process. Bind failure used a scratch runtime with real references to a
    missing member and a missing assembly, on net48 and net8, during activation and during a run.
  - MSTest's `AssemblyResolver` does not hide a bind failure across sessions in a host: after a
    real run in Revit 2024 its handler is gone from `AssemblyResolve`, and the kernel's resolver is
    first during a run. Other add-ins' resolvers stay in the chain and were not involved.
  - Not proved: a real newer MSTest (none exists above the pin; newer binding is proved with a
    compiled fixture), and a bind failure inside `TestApplication.RunAsync`. MTP may turn that into
    an exit code, which the runtime reports as `InvalidOperationException`, so it would be
    `ProviderFailed`, not the sentence.
  - Open for the owner: `dotnet test` shows the exact sentence, but the adapter prints it as
    `System.InvalidOperationException: <sentence>` under `failed Test run failed`, with five
    adapter stack frames, as it does for every runner error. Outcome 5 asks for one sentence.
  - Seen once, not reproduced: the first Revit 2024 launched by a run exited as `dotnet test`
    finished, with no crash log. That run redirected output from PowerShell. Later runner-launched
    runs of MSTest and NUnit on 2024 left the host up.
  - Local leftovers: a stale `bin\...\MSTestRuntime` gets deployed because deploy copies all of
    `TargetDir`, so existing checkouts need it deleted once. `Contents\2026\MSTestRuntime` is still
    in this machine's bundle because Revit 2026 was not rebuilt and the AutoCAD deploy does not
    clear the folder. Nothing reads it.
- 2026-11-07 P3 (verified live on Revit 2024 and 2027):
  - The goal of 0060 holds for TUnit: a copy of the sample built against TUnit 1.81.0, on an
    adapter built against 1.78.0 and the same add-in, gives 60 passed, 1 skipped, 1 failed
    (`Intentional_failure_for_demo`) on both years. A test in that copy read the loaded
    assemblies: `TUnit.Core` and `TUnit.Engine` 1.81.0.0 and MTP 2.5.2.0, from the shadow
    directory on net48 and from the isolated context on net10.
  - S12: the runtime has no direct `Microsoft.Bcl.AsyncInterfaces` or `System.Text.Json`
    reference on net48. It now requests `Microsoft.Bcl.AsyncInterfaces` 9.0.0.0 and no
    `System.Text.Json`; the user's closure supplies 10.0.0.10.
  - S6: the check for the `TUnit` package looks for `TUnit.Engine` in `@(ReferencePath)`, because
    that is what a run needs. A project with `TUnit.Core` and `TUnit.Engine` but not `TUnit` passes.
  - Outcome 5: a stop now reaches `dotnet test` as `failed <sentence>` with no stack. The runner
    returns it as a `TestRunResponse` with `ProtocolIncompatible`, and the adapter publishes one
    error node named by the sentence. Other runner failures keep their detail. MTP's
    `CloseTestSessionResult.ErrorMessage` was tried first and rejected: `dotnet test` showed
    `error: 1` and exit code 10, but not the sentence.
  - A bind failure inside `TestApplication.RunAsync` is not an exit code. Measured outside a host
    on MTP 2.5.2: an exception from the framework or from a data consumer leaves `RunAsync`
    unchanged. A real MSTest run with an older `MSTestAdapter.PlatformServices` threw
    `TypeLoadException` out of `MSTestRuntimeSession.Run`, and `RuntimeBindFailure` explains that
    message. No MSTest runtime change was needed. Not run on a host.
  - The TUnit engine path did hide one: `TUnitTestFramework.ExecuteRequestAsync` catches everything
    and publishes a node named `Unhandled exception - <Type>: <message>`, so the failure read as a
    test result with a stack. `TUnitEngineBindFailure` now rethrows it after the session closes,
    when the type is on the 0060 §4 list and the message names `TUnit.` or `Microsoft.Testing.`.
    It depends on that node name; if TUnit renames it, the failure goes back to being a result.
    Live on 2027 with `TUnit.Engine` 1.72.0 swapped into the 1.81.0 output: the bind sentence, then
    a normal run in the same process.
  - On this machine the installed `%LOCALAPPDATA%\RevitDevTool\current\DevTools.TestRunner.exe`
    wins over the bundle copy that `dotnet publish` deploys, so a local runner change has no effect
    until `DEVTOOLS_TESTING_RUNNER_PATH` points at the new exe. The P3 proof set it. An older runner
    still shows the sentence with the stack, because it sends the stop through stderr.
  - A second run of the same generation in one Revit process fails the sample's
    `Repeat_expands_four_executions`: the counter reached 5 because its static value survived the
    first run. Seen once, and the old path was not run to compare.
  - Local leftovers: `bin\...\TUnitRuntime` folders were deleted once, for the reason given under P2.
    `Contents\2025\TUnitRuntime` and `Contents\2026\TUnitRuntime` remain because those years were
    not rebuilt. Nothing reads them.

## Validation

- Focused: `DevTools.Testing.Host.Tests` / `TUnit.Host.Tests` generation and
  activation tests on net48 and net10 with a fake add-in folder that has no
  `*Runtime\`; manifest reader tests; sentence tests for each S5 stop; a
  `PackageConsumerTests` assertion that framework runtime + manifest land in the consumer output;
  an old-framework-runtime × new-`Abstractions` compatibility test.
- Integration: P6 live runs.
- Repository-required: compile per `.agents/skills/build/SKILL.md`; `dotnet run
  --project tests/…` for touched test projects.

## Result

Not started.
