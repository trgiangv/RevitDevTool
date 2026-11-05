# Execution Plan: Framework Runtime Shims Ship With NetTest (ADR 0060)

Date: 2026-11-06

## Status

Active. Nothing implemented yet. Decision: [0060](../../decisions/0060-framework-runtime-shims-ship-with-nettest.md)
(Proposed). Depends on the `nettest` train of
[2026-11-06-release-trains-0059](2026-11-06-release-trains-0059.md) for shipping;
the code work here can start before that plan's P3.

Visual of the agreed design (owner's private artifact):
https://claude.ai/artifact/Snj9TnxPnrRfTwKTTnrVF2

## Outcome

1. The add-in ships no framework runtime: no `NUnitRuntime\`, `TUnitRuntime\`,
   `MSTestRuntime\`, no framework assembly, no framework version pin.
2. The shims `DevTools.{NUnit,TUnit,MSTest}.Runtime.dll` and a manifest each ship in
   the `RevitDevTool.TestAdapter` nupkg and land in the user's output.
3. Inside the host, the user's framework copy is always used; the framework is
   checked by **major** only.
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
  - Internals the shims use: `NUnit.Framework.Internal` / `Internal.Builders`
    (`source/DevTools.NUnit.Runtime/NUnitFilterFactory.cs`, `NUnitResultMapper.cs`,
    `NUnitAssemblyBuilder.cs`), private `TestContext.DefaultWorkDirectory`
    (`NUnitAssemblyBuilder.cs:107`, verified), `source/DevTools.TUnit.Runtime/TUnitEngineBindings.cs:102-119`,
    `source/DevTools.MSTest.MTP/MtpDiscoveryInternals.cs:25-56` (linked into the runtime).
  - The nupkg already ships testhost siblings `DevTools.{NUnit,TUnit,MSTest}.MTP.dll`
    under `build/runtime/{tfm}`; they share source and pins with the shims
    (`DevTools.NUnit.MTP.csproj:18-23`, `DevTools.TUnit.MTP.csproj:22-26`).
- Pins today: NUnit 5.0.0, TUnit 1.78.0, MTP 2.5.2 (`Directory.Packages.props`),
  MSTest.Sdk 4.5.2 (`global.json`).
- Proof: `.agents/skills/build/SKILL.md`, `.agents/skills/revit-test/SKILL.md`,
  `docs/agents/host-testing.md`, `docs/agents/test-matrix.md`,
  `docs/agents/mcp-integration-test.md`.

## Locked decisions (do not re-open without the owner)

| # | Decision | Why |
|---|----------|-----|
| S1 | Shims and their manifests move from the add-in to the `nettest` nupkg (`build/runtime/{tfm}`), copied to the user's output by `buildTransitive` targets. The add-in keeps only the kernel and `Testing.Abstractions`. | The coupling is shim ↔ framework, both chosen by the user in one csproj; it does not belong in the add-in |
| S2 | The framework is checked by **major** only. The manifest has **one** framework field, `version`, written by the build from the central pin (never by hand); its major is the supported major; the full value appears in messages. | Owner: "giữ version 1 cái thôi tự suy ra major" |
| S3 | **No** `verified` list, **no** online compat index, **no** canary or golden behavior suite as a requirement. | Owner: over-engineering; online index needs GitHub release updates and a network check per run. If a rare minor breaks, the user steps back or updates the adapter |
| S4 | NUnit 5 is the only supported major. Do not design for NUnit 4. | Owner: default is already 5 |
| S5 | Host order: (1) manifest present → (2) `testing.runtime` ranges overlap → (3) framework major matches → (4) load shim + user closure (user copy always wins) → (5) run; a bind failure (`MissingMethodException`, `MissingFieldException`, `TypeLoadException` from the framework) becomes one sentence. Each stop is `ProtocolIncompatible` with the sentence, not `ProviderFailed`, and does not poison the session. The adapter checks 1–3 in the testhost too. | Safe enough with no maintained lists |
| S6 | Build check: `buildTransitive` targets fail the build when the resolved framework package major differs from the manifest. Nothing else at build time. | Early, cheap, deterministic |
| S7 | NUnit stays host-shared, one copy per Revit process; another file version in the same process stops with "Revit has NUnit <loaded> loaded from an earlier run. Restart Revit to run tests with NUnit <requested>." | NUnit static state is per process; no two versions in one process |
| S8 | `Testing.Abstractions` changes by addition only: new capability = new interface detected with `is`; no default interface members (net48); `AssemblyVersion` fixed at `1.0.0.0` while `testing.runtime` `Min = 1`; API compatibility against the last release in CI (`EnablePackageValidation` or `Microsoft.DotNet.ApiCompat`). `Current` +1 for a capability; `Min` +1 only in a new add-in major. | The only cross-train ABI; cheap guard |
| S9 | Moving a framework pin is a normal `nettest` change: move the pin, keep CI green, bump the version, and write in the changelog the framework versions it was built against and any internal-API change. Never an `addin` release. | Owner: internal-flow changes are breaking logic to verify and note in the release, nothing heavier |
| S10 | No legacy (0059 L1): no fallback to shims beside the add-in. An adapter without a manifest gets "RevitDevTool.TestAdapter <v> is not compatible with RevitDevTool <v>. Update RevitDevTool.TestAdapter to the latest version." | Clean redesign |
| S11 | Order in the repo: MSTest → TUnit → NUnit. Users get all three in one `addin` major and one `nettest` release. | Smallest first; users change setup once |

Rejected (with reason) — do not propose again:

- `verified` list or exact pins in the manifest: every framework patch would need a
  `nettest` release.
- Online verified-pairs index in the `channels` release: maintenance plus a network
  dependency per run (owner rejected).
- Shims on public framework API only (NUnit `FrameworkController`, MTP in-process):
  a rewrite larger than the risk.
- The add-in carrying one shim per framework version.
- Keeping the pins with nicer errors.

## Exact sentences (contract; keep wording)

- No manifest: `RevitDevTool.TestAdapter <v> is not compatible with RevitDevTool <v>. Update RevitDevTool.TestAdapter to the latest version.`
- Range mismatch: the 0055 §4 sentence (`<A> is not compatible with <B>. <update message of the older side>`).
- Major mismatch: `<Framework> <found> is not supported by RevitDevTool.TestAdapter <v> (built for <Framework> <major>.x). Update RevitDevTool.TestAdapter to a version for <Framework> <found major>.`
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
  "version": "5.0.0",
  "netfx": { "binds": ["System.Text.Json"], "omit": [] }
}
```

`netfx` holds the closure rules the add-in payload holds today (`NetfxClosureBind`,
the omit list in `NUnitRuntimePayload.targets:19`); the kernel applies them without
framework knowledge.

## Scope

In scope: 0060 §1–§10.

Out of scope: release pipeline (plan 0059), changing 0017 output routing,
public-API rewrites of the shims, NUnit 4.

## Approach

### P0 Spikes (must pass before code)

1. net48: a non-strong-named `Testing.Abstractions` with fixed `AssemblyVersion`
   binds for a shim built against it, when the host loaded its own copy first.
2. Isolated context loads a shim from the shadow directory together with the user's
   closure, applying `netfx` rules from a manifest instead of the payload targets
   (net48 and net10).
3. TUnit.Engine taken from the user's output activates through the shim.
4. MSTest/MTP closure from the user's output works with the linked
   `MtpDiscoveryInternals`.

Record each result under Decisions. If one fails, stop and ask the owner.

### P1 Shared infrastructure

- Manifest model and reader in `DevTools.Testing.Host` (extend the existing
  `ManifestRuntimeSessionFactory` concept); `testing.runtime` range via
  `DevTools.Ipc` `ProtocolRange`.
- Build: each `DevTools.*.Runtime.csproj` writes its manifest at build, `version`
  taken from the central `PackageVersion` (or `global.json` for MSTest.Sdk). No
  version literal in code or comments (owner rule).
- Nupkg: pack shims + manifests into `build/runtime/{tfm}`; `buildTransitive`
  targets copy them to the output and run the major check (S6).
- Host checks S5 1–3, bind-failure mapping (S5 step 5), `ProtocolIncompatible`
  without poisoning the session (`TestingRequestHandler.cs:201-205`).
- `testing/hello` carries the host `testing.runtime` range and add-in version; the
  adapter checks S5 1–3 in the testhost.
- ApiCompat/package validation for `Testing.Abstractions`; freeze `AssemblyVersion`.

### P2 MSTest

- Shim from the user's output; remove `MSTestGenerationPolicy` pins and the
  `MSTestRuntimePayload.targets` `<Error>`; stop shipping `MSTestRuntime\`.
- Remove `MergeRuntimeDependency` override behavior for MSTest (user copy wins).

### P3 TUnit

- Same as P2 for TUnit; TUnit.Engine from the user's output.

### P4 NUnit

- Same as P2 for NUnit; NUnit shared-copy rule and sentence (S7).

### P5 Remove the old path

- Delete `TestingGenerationPins` equality checks, `RuntimeSource.ResolveBeside`,
  the payload imports at `RevitDevTool.csproj:66-68`, and the `*RuntimePayload.targets`
  that only served the add-in.
- Docs, one layer each: `docs/product/host-testing.md`,
  `docs/product/tunit-host-testing.md`, `docs/architecture/` testing pages,
  `docs/agents/host-testing.md`, `docs/agents/test-matrix.md`,
  `.agents/skills/revit-test/SKILL.md` (pin statements).

### P6 Live verification

- Run every framework in Revit years covering net48, net8, and net10, plus AutoCAD
  (per `docs/agents/mcp-integration-test.md` / `host-testing.md`). Include: framework
  minor bump runs; major mismatch → build error; second NUnit version → restart
  sentence; adapter without manifest → sentence.

## Risks And Recovery

- net48 binding or closure rules fail (P0.1/P0.2): stop; ask the owner before any
  fallback (for example strong-naming or a host-side redirect).
- A shim needs a host-only file that used to sit in the payload: add it to the
  manifest `netfx` rules, not back into the add-in.
- Partial landing: frameworks move one at a time in the repo, but nothing ships until
  P6 passes for all three (S11). Until then the add-in keeps the old path for the
  frameworks not yet moved.

## Progress

- [ ] P0 spikes (results in Decisions)
- [ ] P1 shared infrastructure
- [ ] P2 MSTest
- [ ] P3 TUnit
- [ ] P4 NUnit
- [ ] P5 remove old path + docs
- [ ] P6 live verification

## Decisions

- 2026-11-06: Design locked with the owner (table above). Earlier drafts with a
  `verified` list, a `supports.range`, an online index, and golden/canary suites were
  dropped as over-engineering.

## Validation

- Focused: `DevTools.Testing.Host.Tests` / `TUnit.Host.Tests` generation and
  activation tests on net48 and net10 with a fake add-in folder that has no
  `*Runtime\`; manifest reader tests; sentence tests for each S5 stop; a
  `PackageConsumerTests` assertion that shim + manifest land in the consumer output;
  an old-shim × new-`Abstractions` compatibility test.
- Integration: P6 live runs.
- Repository-required: compile per `.agents/skills/build/SKILL.md`; `dotnet run
  --project tests/…` for touched test projects.

## Result

Not started.
