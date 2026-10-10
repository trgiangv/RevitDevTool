# 0042 Removable Test Workarounds After MTP 2.5 / MSTest 4.5

Date: 2026-10-10

## Status

Accepted. Landed 2026-10-10
([plan](../plans/completed/2026-10-10-mtp-2-5-pin.md)). Pins are MSTest
**4.5.1**, Microsoft.Testing.Platform **2.5.1**, and TUnit **1.73.19**.
`MSTestPlatformPolicy.Cancel` is deleted. The other seams in this
decision stay.

Inherits [0021](0021-testing-kernel-and-provider-owned-framework-runtime.md),
[0022](0022-nunit-mtp-only-testing-stack.md), [0035](0035-mstest-sdk-repo-tests.md),
and [0038](0038-mstest-host-provider.md).

## Context

MSTest **4.5.1** (2026-10-07) is the shipped 4.5 line. Tag `4.5.0` was not
released. Microsoft.Testing.Platform **2.5.1** shipped the same day;
functional notes are in **2.5.0** (2026-10-05). TUnit has moved with it.
NuGet `TUnit.Engine` **1.72.16** still depends on MTP **2.4.1**.
**1.73.0** and **1.73.5** depend on MTP **2.5.0**. The latest listing,
**1.73.19** (published 2026-10-08), depends on MTP **2.5.1** and
`Microsoft.Testing.Extensions.TrxReport.Abstractions` **2.5.1** on
`net8.0`, `net9.0`, `net10.0`, and `netstandard2.0`. `main` at
`3ccd9a15c` (2026-10-10) pins the same 2.5.1 packages in
`Directory.Packages.props`. RevitDevTool pins TUnit **1.73.19** with
MTP **2.5.1**. `AddTUnit` remains the public standalone entry;
1.73 does not add a host API that replaces `TUnitEngineBindings`.

Two surfaces consume those stacks:

| Surface | What it is | How MTP starts |
|---------|------------|----------------|
| RevitDevTool in-host | `MSTestRuntimeSession`, `TUnitRuntimeSession`, NUnit runtime inside the host generation | MSTest calls `TestApplication.CreateBuilderAsync`. TUnit builds `TUnitTestFramework` over a hand-made MTP service set and does not call `AddTUnit`. |
| Standalone | In-repo `tests/*.Tests` (MSTest.Sdk, [0035](0035-mstest-sdk-repo-tests.md)) and consumer MTP executables (`dotnet test` / generated entry point, including `RevitDevTool.TestAdapter` testhosts) | MSBuild-generated entry point owns the process. No `InternalMembers` reach into MTP. |

[0038](0038-mstest-host-provider.md) records that
`ITestApplication.RunAsync()` takes no `CancellationToken` and that
`ITestApplicationCancellationTokenSource` is internal. MTP 2.5 adds a
public token on `TestApplicationOptions`. When that token can be
canceled, the platform registers it and calls the same internal
`Cancel()` the in-host policy invokes by reflection
(`TestHostBuilder.ExternallyCancellableHost`).

The other in-host seams were re-checked against the 4.5.1 / 2.5.1
changelog and the current TUnit `AddTUnit` extension. None of them gained
a public replacement.

## Decision

1. **One in-host seam may be deleted, and only together with an MTP 2.5.1
   bump.** `MSTestPlatformPolicy.Cancel` stops reflecting into
   `ITestApplicationCancellationTokenSource`. `MSTestRuntimeSession`
   passes the run `CancellationToken` on `TestApplicationOptions` to
   `TestApplication.CreateBuilderAsync`. `EnableTelemetry = false` stays.
   The observable cancel contract stays the one in 0038: a test body
   already on the host thread is not interrupted; MTP stops scheduling
   the next test and signals `TestContext.CancellationToken`.

2. **Keep every other RevitDevTool reflection and isolation seam.**
   MTP 2.5, MSTest 4.5.1, and current TUnit do not replace them.

   | Seam | Why it stays |
   |------|----------------|
   | `ReflectionMetadataHook.Register` before `AddMSTest` | MSTest 4.5 still resolves the test assembly through that hook. No public "use this `Assembly` instance" API was added. |
   | `MtpDiscoveryInternals` (`_host`, `_discoveredTestsForJson`, stdout silence) | `--list-tests json` still buffers nodes on the platform output device and writes JSON to process stdout. `MSTestListing` still cannot read discovery from `IDataConsumer`. |
   | `MSTestConsoleCapture` and `mstest:output:captureTrace=false` | 4.5 does not stop MSTest from replacing `Console` / `Trace` unless that switch stays off. |
   | `[DependsOn]` closure before `--filter-uid` | Filtered runs still contain only the requested ids. MSTest still orders dependencies only inside that set. |
   | `Logger.OnLogMessage` handler accumulated per `UnitTestRunner` | Not listed as fixed in 4.5.1. The handler still roots a delegate on the loaded framework. |
   | `TUnitSourceCatalog` park/restore | `Sources.TestEntries` is still a process-wide map filled by the module constructor. MTP 2.5 does not scope it per generation. |
   | `TUnitEngineBindings` fake MTP `ServiceProvider` | `AddTUnit()` is already public and still starts a full `TestApplication`. Switching the in-host session onto it would reintroduce process ownership 0038 refused, and would not remove the catalog park. |
   | net4x `ModuleInitializerAttribute` compile in the TestAdapter | TUnit generated code still needs that attribute on .NET Framework. The TUnit tree has no replacement. |
   | Private `MSTestRuntime\` / `TUnitRuntime\` closures and version pins | 2.5.1 is a different `Microsoft.Testing.Platform.dll`. The generation must still fail closed on a mixed copy. |
   | `NUnitAssemblyBuilder` reach into `TestContext.DefaultWorkDirectory` | NUnit in-host does not go through `TestApplication`. The new token does not apply. |

3. **Do not retarget TUnit in-host onto `TestApplication` or
   `ITestingPlatformBuilderConfigurator` as part of the MTP 2.5 bump.**
   0038 already refuses a shared MTP embedding layer generalized from
   the TUnit engine host. Orchestrator middleware
   (`ITestHostExecutionOrchestratorMiddleware`) cannot repeat, shard, or
   rewrite a run, so it is not a stand-in for generation isolation,
   `[DependsOn]` expansion, or cancel. `ExecuteRequestContext.StartTestExecutionAsync`
   is for the framework that publishes nodes. MSTest already does that;
   RevitDevTool stays on `IDataConsumer`.

4. **Standalone tests have no matching reflection seam to delete.**
   In-repo `tests/*.Tests` and consumer MTP executables already enter
   through the generated MTP entry point. A later pin move
   (MSTest.Sdk 4.4.1 → 4.5.1, Microsoft.Testing.Platform 2.4.1 → 2.5.1,
   TUnit 1.72.10 → 1.73.19) is a 0035/0038 pin change,
   not a workaround removal. Do not add `MSTest.Extensions.Hosting`,
   assertion-failure diagnostics, or host-owned OpenTelemetry to those
   projects unless a test author opts in. Those APIs are experimental
   (`TPEXP` on the hosting configurator) or opt-in
   (`mstest:execution:captureAssertionFailureDiagnostics`). 0022 still
   forbids a VSTest adapter on `develop`; the MTP 2.5.1 blocking wait
   inside `Microsoft.Testing.Extensions.VSTestBridge` does not delete
   anything in this repo.

5. **This ADR does not supersede 0035 or 0038.** Those decisions keep
   the provider shape and the MSTest.Sdk harness. Their version sentences
   are amended to the pins above. `MSTestPlatformPolicy` checks 4.5.1
   and 2.5.1.

## Alternatives Considered

1. **Delete the MSTest cancel reflection on 2.4.1.** Rejected.
   `TestApplicationOptions.CancellationToken` is part of the 2.5 host
   builder. 2.4.1 has no public registration onto the cancel source.
2. **Move in-host MSTest and TUnit onto caller-owned
   `Microsoft.Extensions.Hosting`.** Rejected for this decision. The
   Revit `IHost` is the add-in composition root. Test generations need
   a private MTP and framework closure. `RunTestingPlatformAsync` starts
   and stops the caller's host around one MTP run and does not propagate
   services to another testhost process.
3. **Treat TUnit `AddTUnit()` as the removal of `TUnitEngineBindings`.**
   Rejected. That extension is the standalone entry point. The in-host
   session avoids it so Engine execution stays on the host thread with
   `maximum-parallel-tests` forced to 1 and without a nested MTP
   application. The catalog park remains either way.
4. **Bump pins inside this decision.** Rejected. Proof is the existing
   MSTest harness (assembly identity, generation swap, `captureTrace`,
   logger handler, cancel) plus the TUnit binding resolve, run on 4.5.1
   / 2.5.1. That work belongs in a plan.

## Consequences

Positive:

- The bump has a single allowed deletion: MSTest in-host cancel
  reflection.
- Standalone and in-host stays are explicit, so a pin change does not
  get justified as a kernel rewrite.

Tradeoffs:

- Cancel stays on a reflected internal type until that bump.
- TUnit in-host ceremony (`TUnitSourceCatalog`, `TUnitEngineBindings`)
  is unchanged by this release line.
- Standalone projects gain 4.5.1 diagnostics, coverage 18.12.0, and
  reporting fixes only after the SDK pin moves. Nothing in-tree forces
  that move.

## Follow-Up

- Live two-generation Revit proof stays on
  [2026-09-28-mstest-host-provider](../plans/active/2026-09-28-mstest-host-provider.md).
- Pack `RevitDevTool.TestAdapter` only when that nupkg is published. The
  dependency it restores is now Microsoft.Testing.Platform.MSBuild 2.5.1.
