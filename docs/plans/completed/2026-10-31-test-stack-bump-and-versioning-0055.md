# Execution Plan: Test Stack Bump, Then ADR 0055 Versioning

Date: 2026-10-31

## Status

Completed 2026-11-02. Phase 0 (test stack bump) is verified except the items
under "Not proven". Phases 1–8 implement
[0055](../../decisions/0055-product-version-wire-contracts-and-satellite-compatibility.md),
which is **Accepted**. Publishing the adapter and pytest, and deploying the
gateway, are release steps (Release order below).

## Outcome

1. The repo, the in-host generation pins, the consumer docs, and a new
   `RevitDevTool.TestAdapter` move to the current stable MSTest, MTP, and TUnit.
2. The product version, wire contracts, and the satellites (NuGet, pytest,
   McpGateway) follow 0055, each step shippable alone.

## Context

- Decisions: 0055 (this plan implements it), [0042](../../decisions/0042-mtp-2-5-removable-test-workarounds.md)
  (pins; a pin change is a plan), [0035](../../decisions/0035-mstest-sdk-repo-tests.md),
  [0038](../../decisions/0038-mstest-host-provider.md).
- Precedent: [2026-10-10-mtp-2-5-pin](../completed/2026-10-10-mtp-2-5-pin.md).
- Proof commands: `.agents/skills/build/SKILL.md`, `docs/agents/host-testing.md`.

## Phase 0 — Test stack bump

Pins are exact assembly versions in code and tests, so a bump is not props-only.
Touch points: `Directory.Packages.props`, `global.json`, the MSTest
`PackageVersion` in `DevTools.MSTest.{Runtime,MTP}` and the two MSTest fixture
projects, `MSTestPlatformPolicy`, `MSTestRuntimePayload.targets`,
`TUnitGenerationPolicy`, `MSTestGenerationPolicy`, the tests asserting them, and
the docs and skills that state the pins.

| Package | Before | Now | Adapter |
|---------|--------|-----|---------|
| MSTest (`Sdk`, `TestFramework`, `TestAdapter`) | 4.5.1 | 4.5.2 | — |
| `Microsoft.Testing.Platform`, `.MSBuild` | 2.5.1 | 2.5.2 | — |
| TUnit (`TUnit`, `.Core`, `.Engine`) | 1.76.0 | 1.78.0 | — |
| NUnit | 5.0.0 | 5.0.0 (already latest) | — |
| `RevitDevTool.TestAdapter` | 0.1.4 | 0.2.0 | bumped once, with the 0055 work |

Assembly versions equal the package versions (read from the packages):
`TUnit.Core` 1.78.0.0, MTP 2.5.2.0, MSTest framework/adapter/platform-services
4.5.2.0.

### Reflection seams, checked against upstream

Every member the in-host code reaches by reflection was checked in the new
binaries, and the upstream diff was read for each seam
(`thomhurst/TUnit` v1.76.0→v1.77.0→v1.78.0, `microsoft/testfx` v4.5.1→v4.5.2).

| Seam | Result |
|------|--------|
| `TUnitEngineBindings`: `TUnitExtension`, `TUnitTestFramework(IExtension, IServiceProvider, ITestFrameworkCapabilities)`, MTP `ServiceProvider.AddService(object, bool)`, `NopPlatformOutputDevice`, the three session-context constructors | All present in TUnit.Engine 1.78.0 and MTP 2.5.2. No changed line in either diff touches their signatures |
| `TUnitSourceCatalog` over `Sources.*` | `Sources` is not in the 1.76.0→1.78.0 file lists; it compiles. 1.78.0 changes no `TUnit.Engine` or `TUnit.Core` file |
| `TUnitEngineCommandLine`, `TUnitEngineMessageBus` | 1.77.0 reads a new option and publishes a new data type. The fake returns `false` for the option; the bus ignores unknown data |
| MTP `ServiceProvider.GetServiceInternal` rewritten in 2.5.2 | Same first-match semantics; engine run and `TUnit.Runtime.Tests` pass on it |
| MSTest/MTP: `_host`, `ServiceProvider`, `_discoveredTestsForJson`, `OriginalOutputDevice`, `SystemConsole.CaptureConsoleOutWriter` | All present in MTP 2.5.2. None of those names appears in a changed line of the diff. The #11896 JSON change only escapes non-ASCII in the printed JSON; the code reads nodes from the buffer |
| `ReflectionMetadataHook`, `Logger.OnLogMessage`, `captureTrace`, `ExternallyCancellableHost` | Not in any changed line of the diff |

### Verification

- Compile `-c Debug`, 0 warnings, 0 errors: `DevTools.MSTest.{Runtime,MTP}`,
  `DevTools.TUnit.{Runtime,MTP}`, `DevTools.NUnit.MTP`, `DevTools.Testing.Host`,
  `DevTools.TestAdapter` (`ILRepackable=false`).
- `dotnet run --project tests/...`: `Testing.Host` 69/69, `TUnit.Host` 1/1,
  `TUnit.Runtime` 15/15, `NUnit.MTP` 52/52, `Daemon` 107/107, `Mcp.Client` 41/41,
  `MSTest.Runtime` and `MSTest.MTP` PASS lines, `TestAdapter.Tests` 88/89.
  24 of the other 32 `tests/*.Tests` pass; the 8 that fail fail identically at
  HEAD (`AcadDevTool`, `AssemblyIsolation` ×2, `Execution.Pytest`,
  `HandyControl`, `Mcp.Adapter`, `Mcp.Catalog`, `NUnit.Host.NetFramework`).
- **Real engine run.** `TUnit.Runtime.Tests` only resolves bindings and builds
  the framework, so a throwaway harness (not committed) ran a TUnit probe
  assembly through `TUnitRuntimeSession` (pass, fail with message, two
  parameterized cases, console output). Result: the same 5 results at TUnit
  1.76.0 / MTP 2.5.1 and at 1.78.0 / MTP 2.5.2. With the new
  `FailOnUnobservedTaskExceptions` setting forced on, 1.77.0 adds one extra
  `Error` result for the unobserved exception and the bus maps it; 1.76.0 does
  not have the setting.
- `dotnet run --project build -- adapter` packs to `output/nuget` only; nothing is published.

Two defects found at HEAD, not caused by the bump:

- The MSTest fixtures exclude `buildTransitive`, which carries the MSTest
  `global using`, and did not compile. Fixed with a direct `<Using>`.
- `PackageConsumerTests.Packed_package_discovers_tests_for_both_nunit_and_tunit_consumers`
  fails at the net8.0-windows NUnit consumer with `MSB3030 ... apphost.exe`
  (SDK 10.0.400, `--no-restore`).

### Not proven

- net8.0 consumer discovery against the new pins (the `apphost` failure above).
- A live Revit or AutoCAD run of MSTest and TUnit in-host.
- The `FailOnUnobservedTaskExceptions` path with a real user-set value (`TUnitSettings.Default`
  is internal, so the harness set it by reflection).
- Release notes for MSTest 4.5.2, MTP 2.5.2, and TUnit 1.76.1–1.78.0 were read
  as diffs and commit subjects, not as published notes.

### Remaining

- Release note: hosts at or after this release require MSTest 4.5.2 and TUnit
  1.78.0 in the test project; a test project on the old pins fails at generation.

### Release order

1. Product release (Daemon, TestRunner, add-in payloads).
2. Same day: publish `RevitDevTool.TestAdapter 0.2.0` (`PublishTestAdapter.yml`)
   and `revitdevtool_pytest 0.6.0` (PyPI).
3. Deploy McpGateway after most installed Daemons have updated (0055 §10.2).

Release note: *RevitDevTool.TestAdapter 0.2.0 and revitdevtool_pytest 0.6.0
need this RevitDevTool release or later; older adapters and pytest plugins get
a message that names the update. After updating, restart Revit or AutoCAD
before running tests.* A new adapter against an older RevitDevTool shows the
old runner's "Testing protocol version 0 is not supported" (that runner has no
`sender`); later releases name the update.

## Phases 1–8 — ADR 0055

Every range starts at `1` (testing, pytest, tunnel).
The `sender` shape breaks shipped clients on purpose (0055 §2); they get the
sentence.

| # | Step | Where | Proof |
|---|------|-------|-------|
| 1 | One product-version accessor; fix `ShellViewModel` (`ToString(3)` drops `-beta.N`) and `SentryTelemetry` | `DevTools.Utilities/AppVersion.cs`, `ShellViewModel`, `SentryTelemetry` | Unit test: suffix round-trips; compile the Revit host |
| 2 | `ProtocolRange` and one `sender` per message; `testing/hello` and `ProtocolIncompatible` | `DevTools.Ipc/ProtocolRange.cs`, `TestingProtocol.cs`, `TestPipeClient`, `TestingRequestHandler` | Property test of `A.Min ≤ B.Current && B.Min ≤ A.Current`; a message without `sender` gets the sentence |
| 3 | Golden fixtures and cross-version CI matrix | `tests/DevTools.Testing.Transport.Tests`, `Compile.yml` | Last published adapter and runner against the HEAD host. First read what `TestingProtocolGoldenTests` covers |
| 4 | `instance/info` fields and the pytest gate | `InstanceInfo.cs`, `PytestContracts`, PyTest `bridge.py` | `revitdevtool_pytest 0.5.0` gets the sentence; a new client rejects an out-of-range host |
| 5 | Adapter release stays manual | `DevTools.TestAdapter.csproj` `<Version>`, `PublishTestAdapter.yml` | — |
| 6 | Tunnel `register` ranges and `register_ack` | Daemon tunnel client, `McpGateway/src/{tunnel-relay,types}.ts`, `docs/api.md` | Gateway deployed first (0055 §10.2); gateway repo tests |
| 7 | Daemon holds a major swap while a selected payload is pending | `DevTools.Update/Engine/{UpdateEngine,DaemonUpdater}.cs` with 0049 | `DevTools.Update.Tests` |
| 8 | One mismatch sentence in the client output; no Daemon notification | `TestingProtocol`, `PytestProtocol`, PyTest `protocol.py`, gateway `tunnel-protocol.ts` | Shipped clients print the host sentence (`TestPipeClient.cs:231`, `bridge.py:211`) |

Pytest-specific work (single version source, `publish.yml` gates, one pytest
range) is not in 0055 yet. Decide in step 4 whether it becomes a 0055
subsection or a plan in the PyTest repo.

## Risks And Recovery

- **A host with the new pins breaks test projects on the old pins** (generation
  fails closed). A user reaches this by updating RevitDevTool alone. The pin
  failure message must name the package, the version, and the fix; 0055 §10.1
  makes that a contract.
- **Assembly version equals the package patch version**, so every patch bump
  edits all the touch points above. Single-sourcing the constants is an open
  follow-up; loosening the exact pin is out of scope (it weakens 0042).
- Rollback of Phase 0: revert the commit. `0.1.4` stays listed on nuget.org.

## Progress

- [x] Phase 0: bump, upstream seam review, real engine run, docs
- [x] Bump the adapter to `0.2.0` and pytest to `0.6.0` (publishing is the release step)
- [x] Amend and accept 0055
- [x] 1 Version accessor
- [x] 2 Ranges with one `sender` per message, with the host-authored text of step 8
- [x] 3 Shipped-peer fixtures (`ShippedPeerTests`); a CI cross-version run needs a host app, so it stays manual
- [x] 4 `instance/info` and pytest gate, in both repos
- [x] 5 Dropped: no automatic adapter gate; the adapter is bumped and published by hand (0055 §5)
- [x] 6 Tunnel ranges, both repos (gateway not deployed)
- [x] 7 Daemon major-update hold (`UpdateEngine.HoldsMajorUpdate`)
- [x] 8 Mismatch sentence in the client output; the Daemon notification is dropped (0055 Alternatives 6)

## Decisions

- 2026-10-31: Keep the exact assembly-version pin for this bump.
- 2026-11-01: The adapter is not bumped for a pin change. It is bumped once,
  when the 0055 versioning work is finished.
- 2026-11-01: TUnit moved to 1.78.0 in the same Phase 0 after the seam review
  above.
- 2026-10-31: Phase 0 lands before any 0055 step; it needs no wire change.
- 2026-11-02: One `sender { version, protocol { current, min } }` per checked
  message (testing `hello` both ways, `TestRunExecute`, pytest runs, tunnel
  `register`) replaces `protocol_version`, `min_protocol_version`,
  `client_version`, and `product_version`. `TestRunRequest` has no version;
  `hello` decides. No legacy path: a message without `sender` is too old and
  gets the sentence. `ProtocolRange`, `ProtocolSender`, and `ProtocolPeer` live
  in `DevTools.Ipc` (already the JSON layer), so `DevTools.Utilities` keeps no
  dependencies. `AppVersion` lives in `DevTools.Utilities`; Transport references
  it and the adapter merges it (its linked `AppUtils.cs` is gone).
  `DevTools.Telemetry` stays on Hosting and Sentry only (0018) and reads the
  informational version of its own assembly.
- 2026-11-01: `instance/info` lists only the `pytest` range. `DevTools.Execution`
  does not reference the testing transport, and testing clients get the range
  from `hello`.
- 2026-11-01: `revitdevtool_pytest` checks the range right after it connects
  and ends the session with `pytest.exit`. An `instance/info` failure does not
  block the run; the host still checks the range on `tests/run`.
- 2026-11-01: One version per process, `AppVersion.Current` (the
  `DevTools.Utilities` assembly; merged into the adapter, so there it is the package version,
  checked in the packed nupkg). Shown in the Daemon window, the Revit pane and
  floating window, and the AutoCAD palette titles.
- 2026-11-01: Each message type has `TestingProtocol.Mismatch(message)` /
  `PytestProtocol.Mismatch(request)`, which returns one short sentence naming
  both sides and the update message, or `null`. The machine `remedy` field of 0055
  §4 is dropped until the Daemon notification (step 8) reads it.
- 2026-11-01: Live check on Revit 2025 (`F:\Project1.rvt`, dev build `1.0.0`):
  dock pane title `DevTools 1.0.0`; `instance/info` returns `productVersion`
  and `protocols.pytest {1,1}`; NUnit sample passes through the new adapter and
  dev runner, and through the shipped `4.0.0-beta.4` runner; pytest smoke 2/2
  with the new plugin; a `tests/run` at protocol 3/min 2 gets the mismatch
  sentence. The Daemon title is checked by `WpfDesktopTests`, not live.
- 2026-11-01: Full run on Revit 2025 with the local runner and the packed
  adapter, TUnit 1.78.0: NUnit 71 pass / 2 fail, TUnit 59 / 1, MSTest 35 / 0;
  every failure is a sample that fails on purpose. pytest `tests/Revit` 44 pass /
  21 fail, the same 21 with the PyTest code at HEAD (schedule tests need a model
  with schedules; image export hits a pythonnet list conversion).
  `tests/Revit_Ipy` 8 pass / 1 skip.
- 2026-11-01: Step 6 uses a close code instead of `register_ack`. After
  `register`, the Daemon hands the socket to the MCP server, so a gateway frame
  would land in the JSON-RPC stream. The gateway closes with `4001` and a
  one-sentence reason; the Daemon stops reconnecting and shows
  "Not compatible with the gateway". `register` carries `sender`.
- 2026-11-02: The automatic adapter gate is removed. Whether an adapter change
  matters to consumers is a judgment, and the packed assemblies are not
  byte-stable, so no build step can decide it. `PublishTestAdapter.yml` is back to
  its manual form.
- 2026-11-02: Test cleanup. The two long-failing tests had real causes:
  `PackageConsumerTests` (net8 consumer) failed because its isolated, empty
  package cache meets the first restore before the package props set
  `OutputType=Exe`, so the net8 apphost is not downloaded. That consumer now
  declares `OutputType=Exe` like the other consumers in the same test; samples
  and docs stay as they were, since the package props cover real consumers.
  `Run_ExecutesRunnerAndReturnsCollectionError_ForMissingNode` expected exit
  code 1; pytest returns 4 for a missing node, and the runner returned no reason,
  so `PytestRunner.py` now adds one. Removed the adapter's self-check of its own
  request version and its test, and merged duplicate hello tests.
- 2026-11-01: Unknown JSON members are ignored by `TestingJsonContext` (tested).
- 2026-11-02: Live check of the sender model on Revit 2025 (`F:\Project1.rvt`,
  dev build `1.0.0`): NUnit 2/2, TUnit 1/1, MSTest 1/1 through the new adapter,
  runner, and add-in; pytest smoke 2/2 and `tests/Revit_Ipy` 8 pass / 1 skip.
  Shipped peers get the sentence: pytest `0.5.0`, adapter `0.1.4` with the new
  runner, and adapter `0.1.4` with runner `4.0.0-beta.4` against the new add-in.
  The new adapter with runner `4.0.0-beta.4` gets that runner's own message; it
  is accepted as the one-time cost of the break (no leg-1 check from the adapter).

## Result

Complete after Phases 1–8. Record the verified outcome, limitations, and
follow-up before moving the plan to `docs/plans/completed/`.
