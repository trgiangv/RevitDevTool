# 0016 Native NUnit Runtime With MTP-First Integration

Date: 2026-08-12

## Status

Accepted. Supersedes the NUnit execution strategy, VSTest-first priority, and
debugging deferral in
[`0015-nunit-host-testing-standard-integration.md`](0015-nunit-host-testing-standard-integration.md).

Implementation notes (2026-08-14 / 2026-08-27): `NUnitReflectionRunner` is
gone. The DevTools VSTest adapter is removed on `develop`
([0022](0022-nunit-mtp-only-testing-stack.md));
`samples/ricaun.NUnit.SampleTests` is a third-party VSTest comparison only.
Test stdout vs host pane: [`0017`](0017-nunit-host-test-output-routing.md).
Visual Studio host Debug: [`0025`](0025-runner-owned-visual-studio-host-attach.md)
keeps EnvDTE in `DevTools.TestRunner.Core/Debugging/`.

## Context

The experimental NUnit host integration currently executes a small NUnit
attribute subset through `NUnitReflectionRunner`. It finds attributes by name,
constructs fixtures, invokes setup and teardown, runs methods through
reflection, and translates selected NUnit exception names into DevTools
results. Extending that runner to support theories, data sources, fixture
sources, retry, repeat, ordering, setup fixtures, async lifecycle, property
bags, framework filters, and future NUnit behavior would make DevTools own a
second and permanently incomplete implementation of NUnit semantics.

The reflection runner was introduced after an in-process NUnit Engine spike
bound to a host-loaded `nunit.framework` identity in .NET Framework Revit and
failed with `FileLoadException`. Shadow-copying test files made rebuilds
possible without locking the build output, but shadow copy is a file-placement
strategy, not a dependency-isolation boundary. `Assembly.LoadFile` and
`Assembly.Load(byte[])` can load new generations in a .NET Framework AppDomain,
but old assemblies, static state, event handlers, threads, and native modules
remain until the host exits.

Modern Revit hosts provide collectible `AssemblyLoadContext`; Revit 2022-2024
remain on .NET Framework 4.8, where only an AppDomain can be unloaded. A child
AppDomain is not a general host-test solution because arbitrary Revit/AutoCAD
API objects and fixture state cannot safely cross that boundary.

MTP and IDE integrations are northbound test-platform concerns. They do not
isolate framework assemblies inside Revit or AutoCAD and must not own Autodesk
host-process lifecycle. Source generation can provide static metadata, but it
cannot reproduce dynamic NUnit discovery or execution semantics.

The supported-tooling floor is based on current vendor evidence, not on a
claim that VSTest has disappeared everywhere:

- Microsoft documents MTP as a lightweight VSTest alternative for CLI, CI,
  and Visual Studio Test Explorer, with support for .NET Framework 4.6.2 and
  later:
  <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro>.
- Visual Studio Test Explorer supports the direct MTP protocol from 17.12;
  older Visual Studio versions fall back to the VSTest protocol:
  <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-vs-vstest>.
- MTP v2 no longer supports its former VSTest-based `dotnet test` target:
  <https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-migration-from-v1-to-v2>.

This evidence supports an explicit MTP-only product floor. It does not prove
that every IDE version or every third-party test consumer no longer needs
VSTest; those consumers are intentionally outside this product's support
matrix.

## Decision

1. **Real NUnit owns NUnit semantics.** DevTools will replace
   `NUnitReflectionRunner` with a framework driver that delegates discovery and
   execution to the supported public NUnit framework API. The preferred
   production API is `NUnitTestAssemblyRunner` / `ITestAssemblyRunner` with an
   NUnit listener and filter. NUnitLite may be used for an isolated proof or
   compatibility fallback, but DevTools will not parse NUnit console text as
   its primary result contract.

2. **No new reflection emulation.** No additional NUnit lifecycle, data-source,
   retry, ordering, assertion, or result semantics may be implemented in the
   reflective runner. The existing subset remains only as a rollback path
   while the native driver is unproven and is removed after the native runtime
   passes its host matrix.

3. **Framework runtimes sit behind a neutral boundary.** Shared host code must
   not expose or exchange NUnit types. A framework bootstrap receives neutral
   request DTOs and emits neutral discovery, progress, output, attachment, and
   result DTOs. The host invokes the bootstrap through a shared DevTools
   contract or a deliberately reflection-based bootstrap boundary; NUnit types
   never cross that boundary.

4. **Modern hosts use one collectible framework ALC per generation.** On
   `net8.0-windows` and `net10.0-windows`, the bootstrap, test assembly,
   NUnit runner component, and private test dependencies are loaded into one
   collectible `AssemblyLoadContext`. `nunit.framework` 4.6 is host-shared into
   a non-collectible Plugin/Default context so Dynamo's copy is not bound.
   Autodesk host APIs and a minimal DevTools contract allowlist resolve from
   the default context. CLR collectible unload is cooperative: live Autodesk
   hosts may retain the ALC (`generation.retained`) because ThreadPool /
   `AsyncLocal` roots survive `Unload()`. That is expected, not a closable
   defect. The modern-host gate is **generation isolation** (new
   `generation_id` plus new IL on the same host PID). Report retained
   generations rather than claim unload from `Unload()` alone. A quiet-process
   weak-reference collection test may still observe unload; it is not required
   live.

5. **.NET Framework uses a coherent, non-unloadable generation.** On `net48`,
   every framework generation is copied into one generation directory and
   loaded without a normal load-context dependency on the host's NUnit copy.
   Resolution is keyed by `ResolveEventArgs.RequestingAssembly` and the owning
   generation, never by simple name plus the latest output directory. Runs are
   serialized, generations are immutable after publication, and old
   generations are retained until host exit. This is controlled coexistence,
   not isolation equivalent to ALC.

6. **The net48 support gate is empirical.** Revit 2024 with Dynamo already
   loaded must prove native NUnit discovery and execution with conflicting
   NUnit identities. The fixture matrix must include `TestCaseSource`,
   `TestFixtureSource`, setup fixtures, async setup/test/teardown, retry,
   repeat, explicit/ignore, categories/properties, generic and parameterized
   fixtures, assertion output, cancellation, and two rebuilt generations. If
   the coherent-generation design cannot pass, DevTools will document host
   restart or reduce the supported net48 environment; it will not restore
   reflection emulation as the full-feature endpoint.

7. **Full framework semantics do not imply NUnit Engine process hosting.** Tests
   still execute inside the Autodesk host through `IHostContextExecutor`.
   Multi-process agents and Engine-managed AppDomains are out of scope. NUnit
   worker parallelism defaults to one because Autodesk APIs require serialized
   host-context execution; broader parallel execution requires a separate
   safety decision and host proof.

8. **MTP is the only supported IDE/test-platform integration.** After the
   native host runtime passes its first live gate, DevTools exposes discovery,
   selection, execution, cancellation, output, and attachments through an MTP
   extension. The public consumer package is **`DevTools.NUnit`** (project
   `DevTools.NUnit.Mtp`), not `DevTools.NUnit.TestAdapter`. The experimental
   VSTest adapter is removed rather than retained as a compatibility bridge.
   The MTP assembly may start the installed **Runner CLI as a one-shot child**
   (same as the unpublished adapter). It must not locate, launch, reuse, or
   kill Autodesk hosts — Runner still owns that policy. A long-lived
   `Runner serve` endpoint is not required to replace VSTest.

9. **Source generation is optional metadata only.** A source generator may be
   introduced only when it measurably improves stable source navigation,
   project opt-in, or static IDs. It must not decide which NUnit cases exist,
   expand data sources, schedule tests, or replace NUnit discovery. Runtime
   NUnit discovery remains authoritative.

10. **The DevTools MTP extension owns no process lifecycle.** The MTP project
    contains no process activation, host locator, launch, reuse, termination,
    or debugger-attach implementation. It may pass Runner `--debug-parent-pid`
    when the northbound process already has a debugger
    (decision 11); it does not reference EnvDTE or attach to the Autodesk host.
    It translates IDE/Test Explorer requests into the
    installed Runner protocol through a transport supplied by the hosting
    package. Runner and its launcher infrastructure own Runner activation and
    Autodesk host-session policy. Runner distinguishes a host it launched from
    a pre-existing host it merely reused. MTP never treats Runner, Revit, or
    AutoCAD as its child.

11. **Visual Studio host-process debugging is Runner-owned EnvDTE attach.**
    MTP 2.3 has no public API for `client/attachDebugger`. Until it does,
    Test Explorer **Debug** is implemented as: MTP passes `--debug-parent-pid`
    when the northbound process already has a debugger attached (that flag implies
    debug); Runner locates or launches the Autodesk host, then attaches the
    Visual Studio instance that is debugging that parent PID to the host PID
    **before** `testing/run`, and detaches after
    ([0025](0025-runner-owned-visual-studio-host-attach.md)).
    `Microsoft.VisualStudio.Interop` and EnvDTE live only in
    `DevTools.TestRunner.Core/Debugging/`. MTP and the host pipe protocol do
    not reference Interop, do not define a generic debugger *wire* protocol,
    and do not add a `debug-ready` handshake. Attach failure warns and the run
    continues. Runner does not attach other IDEs. Revisit this placement if
    MTP exposes a public attach-debugger API.

12. **Debug-visible generations remain file-backed.** Runtime generations use
    coherent shadow directories and file-backed module/PDB paths even when no
    debugger feature is installed. This keeps Visual Studio attachment able to
    resolve test modules without adding debugger behavior to the runtime protocol.

13. **Native module ownership is explicit.** A `LoadLibrary` call owns one
    reference and must never be balanced by repeated `FreeLibrary` calls.
    Owned handles use one-release `SafeHandle` semantics where unloading is
    proven safe. A native module used by a non-unloadable net48 managed
    generation remains loaded until host exit rather than being forcibly
    unloaded underneath managed code.

## Priority And Gates

1. **P0 — Native NUnit runtime:** neutral bootstrap, real NUnit discovery/run,
   modern collectible ALC, coherent net48 generation, and representative
   framework-semantics fixtures.
2. **P0 — Live conflict and reload proof:** Revit 2024 plus Dynamo conflict,
   Revit 2025/2026 generation isolation (new id + new IL; live
   `generation.unloaded` is not required), Revit 2027 compile plus net10 tests
   when the host is not installed, and two-generation rebuild behavior.
3. **P1 — MTP-only integration:** one MTP surface backed by Runner and the
   native framework driver; remove the experimental VSTest adapter.
4. **P1 — IDE run matrix:** Visual Studio Test Explorer for MTP samples.
5. **P2 — Visual Studio Debug:** Runner `--debug` attach-before-run (EnvDTE
   per [0025](0025-runner-owned-visual-studio-host-attach.md)). Python
   `debugpy` remains a separate listen-on-port path; do not invent a generic
   debugger *wire* protocol.
6. **P2 — Optional ergonomics:** source-navigation metadata and source
   generation only after measured need.

Publication is blocked until both P0 gates pass. IDE branding or automatic
attach convenience cannot precede correct NUnit execution.

## Alternatives Considered

1. **Expand `NUnitReflectionRunner` until it resembles NUnit.** Rejected because
   it duplicates framework semantics, permanently lags NUnit, and still does
   not solve assembly isolation.
2. **Use MTP or a source generator to replace NUnit runtime execution.**
   Rejected because MTP is an orchestration platform and source generation
   cannot reproduce runtime data sources or NUnit lifecycle behavior.
3. **Run NUnit Engine or an agent outside Revit.** Rejected because tests must
   execute within the Autodesk host API context; moving execution outside the
   host loses that context.
4. **Use a child AppDomain for every net48 run.** Rejected as the general path
   because Autodesk API objects and arbitrary fixture state cannot cross the
   AppDomain boundary safely. A future restricted proxy model would require a
   separate decision.
5. **Retain VSTest as a compatibility bridge.** Rejected because the package is
   unpublished, Visual Studio 17.12+ has an MTP route, and a second adapter
   duplicates mapping, packaging, filtering, cancellation, and
   provider-ownership risks. Third-party VSTest remains only as
   `samples/ricaun.NUnit.SampleTests`.
6. **Create a generic debugger attach protocol.** Rejected because attach
   ownership and completion semantics belong to the IDE debugger. A generic
   layer would either expose the least-common denominator or recreate
   IDE-specific behavior behind misleading abstractions.

## Consequences

Positive:

- NUnit remains the single authority for its discovery and execution behavior.
- Modern hosts isolate rebuilt generations without restarting the process.
  Live ALC collection is best-effort and may remain `generation.retained`.
- net48 limitations are explicit, measurable, and cannot silently turn into
  more reflection emulation.
- MTP and CLI reach one Runner/host protocol and result model without a VSTest
  compatibility layer.
- All Autodesk host locate/launch/reuse remains in Runner. MTP never treats
  Runner, Revit, or AutoCAD as its child. Visual Studio EnvDTE attach is also
  Runner-owned (`--debug`), not an MTP/Interop package dependency.

Tradeoffs:

- net48 retains loaded managed generations and may require periodic host
  restart.
- Autodesk host safety limits NUnit worker parallelism even though attributes
  and other framework semantics remain NUnit-owned.
- Visual Studio older than 17.12 and other VSTest-only consumers are outside
  the supported tooling floor.
- Visual Studio host Debug uses EnvDTE until MTP exposes `client/attachDebugger`.
  Attach failure does not fail the test run. Other IDEs attach the host
  themselves ([0025](0025-runner-owned-visual-studio-host-attach.md)).
- File-backed shadow generations consume temporary disk space until a safe
  cleanup policy can remove generations no longer used by any process.

## Follow-Up

Native runtime, MTP-only stack, and Visual Studio host Debug are shipped
([0022](0022-nunit-mtp-only-testing-stack.md),
[0025](0025-runner-owned-visual-studio-host-attach.md)). Historical plan:
[`2026-08-12-nunit-native-runtime-mtp.md`](../plans/completed/2026-08-12-nunit-native-runtime-mtp.md).
