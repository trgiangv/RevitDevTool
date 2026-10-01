# 0038 MSTest Host Provider

Date: 2026-09-28

## Status

Accepted. NUnit stays the default engine. TUnit stays the opt-in provider
described in [`host-testing.md`](../product/host-testing.md). MSTest is the
third opt-in provider (`TestingFramework=mstest`). The live two-generation
Revit check remains open on
[`2026-09-28-mstest-host-provider`](../plans/active/2026-09-28-mstest-host-provider.md).

Inherits [0021](0021-testing-kernel-and-provider-owned-framework-runtime.md)
and [0024](0024-testing-core-open-closed-providers.md). A third engine is a
Runtime assembly, an MTP sibling, and a folder under `DevTools.Testing.Host`.
It is not a new kernel.

## Context

In-host TUnit works, including on net48, by compensating for a process-global
catalog. `DevTools.TUnit.Runtime` owns `TUnitSourceCatalog` (park/restore of
`TestEntries` and the hook maps), `TUnitCatalog`, `TUnitExpansion`, a reflected
`TUnitTestFramework`, and a fake MTP service set in `TUnitEnginePlatform`.
That is the ceremony. The testing kernel itself (`FrameworkProvider`,
generation store, `ITestingRuntimeSession`) is already small and shared with
NUnit.

MSTest 4.4 can take the assembly instance the kernel already loaded.
`ReflectionMetadataHook.Register` publishes that instance to
`SourceGeneratedFileOperations.LoadAssembly`. An empty type map still resolves
the assembly; discovery then falls back to ordinary reflection
(`GetDefinedTypes` returns the fallback when the registered type list for that
assembly is empty). A later `Register` for the same simple name replaces the
previous instance. The composite still retains older providers, so the MSTest
assemblies must not be process-wide singletons.

`tests/DevTools.MSTest.Runtime.Tests` is the proof layer for this provider.
It is created with `DevTools.MSTest.Runtime` on MSTest 4.4.1 and
Microsoft.Testing.Platform 2.4.1. There is no standalone spike. That project
must show:

- `LoadFile` + empty `Register` + `TestApplication` / `AddMSTest` executes
  that exact assembly (`ReferenceEquals`).
- Requesting generation A's path after registering B executes B.
- `mstest:output:captureTrace=false` leaves `Console.Out`, `Console.Error`, and
  `Trace.Listeners` unchanged across two runs.
- `Logger.OnLogMessage` gains one handler per `UnitTestRunner` and never
  removes it. The handler reads `TestContext.Current`; it does not root the
  test assembly. It does root a delegate on the `MSTest.TestFramework` that
  was loaded.

`ITestApplication.RunAsync()` takes no `CancellationToken`. MTP cancel is
`ITestApplicationCancellationTokenSource.Cancel()`, and that interface is
`internal`.

The new flow is pinned to MSTest **4.4.1** and Microsoft.Testing.Platform
**2.4.1** together. 4.5 is preview and is not the pin. NUnit, TUnit, and the
in-repo testhost use Microsoft.Testing.Platform 2.4.1 and MSTest.Sdk 4.4.1
([0035](0035-mstest-sdk-repo-tests.md)).

## Decision

1. **Ship MSTest as a third provider on the existing kernel.**
   `TestingFramework=mstest`. NUnit remains the default. TUnit is unchanged.
   Do not delete TUnit and do not generalize TUnit's engine host into a shared
   MTP embedding layer.

2. **The in-host session is a public MTP application over the preloaded
   assembly.** Inside the generation, `MSTestRuntimeSession` does only this:

   ```text
   kernel already loaded testAssembly
        │
        ▼
   ReflectionMetadataHook.Register(testAssembly, empty types, empty methods)
        │
        ▼
   TestApplication.CreateBuilderAsync(testconfig, filter-uid)
        │
        ├─ AddMSTest(() => [testAssembly])
        ├─ consume TestNodeUpdateMessage
        └─ RunAsync()
        │
        ▼
   TestCaseResult
   ```

   MSTest owns discovery semantics, fixtures, data rows, initialize/cleanup,
   timeouts, and result nodes. RevitDevTool owns generation identity, the
   host thread (`IHostContextExecutor` already wraps `testing/run`), and
   neutral results. No `MSTestEngine` reflection. No fake `IServiceProvider`.
   No replica of `TUnitSourceCatalog`.

3. **One generation, one MSTest closure.** The runtime payload is private to
   the generation, same isolation plan as TUnit: `AssemblyIsolationKind.Isolated`,
   and `WithDistinctFileIdentity()` on net48. `ReflectionMetadataHook`,
   `PlatformServiceProvider`, and `Logger.OnLogMessage` then live on that
   generation's copies. Unload on net48 is still impossible; the active
   generation does not share those statics. Do not load MSTest into the host
   default context.

4. **Host wiring is the TUnit trio, not a new subsystem.**

   ```text
   testing/run
        │
        ▼
   MarshaledTestRequestHandler → TestingRequestHandler
        │
        ▼
   TestingProviderRegistry
        │
        ├─ FrameworkProvider (NUnit + selection adapt)
        ├─ FrameworkProvider (TUnit)
        └─ FrameworkProvider (MSTest)
                 │
                 ├─ MSTestGenerationPolicy
                 ├─ ManifestRuntimeSessionFactory
                 └─ TestingRuntimeSessionManager
                          │
                          ▼
                    DevTools.MSTest.Runtime.MSTestRuntimeSession
                    (loaded by type name inside the generation)
   ```

   `AddTestingHostServices()` gains `AddMSTestHostServices()`. Revit and
   AutoCAD already call `AddTestingHostServices()`; there is no host-specific
   MSTest branch. `FrameworkProvider` is reused as-is.

5. **Pin MSTest 4.4.1 and Microsoft.Testing.Platform 2.4.1 for the whole new
   flow.** That pair is closed. `DevTools.MSTest.Runtime`,
   `tests/DevTools.MSTest.Runtime.Tests`, `DevTools.MSTest.MTP`, and the
   generation payload all reference those versions. NUnit, TUnit, and MSTest
   share the repo pin Microsoft.Testing.Platform 2.4.1.

   Generation validation requires exactly one of each:

   | Assembly | Version |
   |----------|---------|
   | `MSTest.TestFramework.dll` | 4.4.1 |
   | `MSTest.TestAdapter.dll` | 4.4.1 |
   | `MSTestAdapter.PlatformServices.dll` | 4.4.1 |
   | `Microsoft.Testing.Platform.dll` | 2.4.1 |

   `Microsoft.Testing.Platform.MSBuild` on the `mstest` testhost sibling is
   2.4.1 as well. `Register` is called only inside `DevTools.MSTest.Runtime`,
   after that check. A mismatch fails the generation; it does not fall
   through to `Assembly.LoadFrom`. Preview 4.5 is out.
   [0035](0035-mstest-sdk-repo-tests.md) is not amended.

6. **Fixed testconfig, no second output stack.** Every in-host run passes
   `--config-file` with:

   ```json
   {
     "mstest": {
       "parallelism": { "enabled": false },
       "output": { "captureTrace": false }
     }
   }
   ```

   No runsettings file and no `<EnvironmentVariables>` section, so MSTest's
   environment-variable host controller must not relaunch the process. Host
   output capture stays `TestingRunTraceScope`
   ([0017](0017-nunit-host-test-output-routing.md)). MSTest must not become
   the process `Console` or `Trace` owner.

7. **Selection v1 is the neutral set TUnit already maps.** `All`, `Names`,
   and `TestIds`. `TestingDiscovery.Register` falls back to the default run mapper (shared with
   TUnit) when a provider passes none; `Discover` resolves `Names` to `TestIds`. Those ids are the MTP `TestNode` uid from
   `--list-tests`. The session passes them as `--filter-uid`. An empty
   `TestIds` list runs nothing. `FrameworkFilter` returns
   `testing/invalid_request`, same as TUnit. No NUnit XML filter and no
   VSTest `FullyQualifiedName` expression.

8. **Cancel is the one internal MTP call.** `RunAsync()` has no token. The
   session cancels by calling `Cancel()` on the generation's
   `ITestApplicationCancellationTokenSource`. That call stays in one method
   next to the 4.4.1 / 2.4.1 version check. It must not signal CTRL+C and must not
   touch `MSTestEngine`. If 4.4.1 exposes a public cancel, use that and
   delete the internal call.

9. **Discovery stays a testhost sibling.** `DevTools.MSTest.MTP` implements
   `ITestDiscoverer` and `ITestRunMapper`. List mode is
   `TestApplication` with `--list-tests` in the short-lived testhost, not a
   second attribute scanner and not execution inside Revit. Run still goes
   through TestRunner and `testing/run`. Adapter `.props` / `.targets` gain
   one `mstest` row. `TestFrameworkId` gains `MSTest`. Those are the kernel
   edits. No new abstractions project, no switch inside
   `DevTools.Testing.Abstractions` beyond the enum value.

## Alternatives Considered

1. **Keep investing in TUnit's catalog.** Rejected for new work. The catalog,
   park/restore, and fake MTP services are the cost of hosting TUnit. They
   stay for suites that already use TUnit.

2. **Reflect `MSTestEngine` or `MSTestTestFramework` directly.** Rejected.
   `MSTestEngine` pulls internal sinks, filters, and recorders. That recreates
   `TUnitEngineHost`. `MSTestTestFramework` still needs the MTP services Route A
   gets from `TestApplication`.

3. **Shared `DevTools.MTP.Embedded` used by TUnit and MSTest.** Rejected.
   TUnit's fake service set and MSTest's public application are different
   shapes. A shared host would be a second framework.

4. **New AppDomain per generation.** Rejected. The kernel already isolates by
   ALC and, on net48, by distinct file identity. The hook removes the
   `LoadFrom` reason to add an AppDomain.

5. **MSTest.Sdk as the testhost entry, with no `DevTools.MSTest.MTP`.**
   Rejected. MSTest.Sdk would execute in the testhost process. Host testing
   discovers locally and executes through `testing/run`.

## Consequences

Positive:

- In-host MSTest does not learn MSTest's fixture or hook object graph.
- Generation identity stays in `TestingGenerationStore` /
  `ManifestRuntimeSessionFactory`, the same path as TUnit.
- Serial execution and "do not capture console" are the run's testconfig,
  not new host services.
- Both Revit and AutoCAD-family hosts pick the provider up through
  `AddTestingHostServices()`.

Tradeoffs:

- `ReflectionMetadataHook` is public infrastructure, not a compatibility
  contract. The new flow stays on MSTest 4.4.1 and MTP 2.4.1 until a
  deliberate bump re-runs the three assembly-identity checks.
- `Logger.OnLogMessage` still accumulates inside one generation if that
  generation's session runs more than once. A new generation gets a new
  `MSTest.TestFramework`. Reusing one generation for many runs can grow
  handlers on that copy. Acceptable; do not add an unhooker unless a host
  run shows the delegate graph retaining the test assembly.
- The composite of a long-lived generation retains each `Register`. One
  register per session start is enough; do not register on every test.
- net48 still cannot unload the previous generation.
- Data-row identity comes from MSTest `--list-tests`. Discovery and
  execution must agree on the MTP node uid. That agreement is a test, not
  a custom id scheme.

## Follow-Up

- Architecture and product docs describe `TestingFramework=mstest`.
- A live Revit run of two generations is the remaining plan gate, not a
  separate ADR.
