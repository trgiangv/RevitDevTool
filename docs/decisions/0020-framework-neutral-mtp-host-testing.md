# 0020 Framework-Neutral MTP Host Testing

Date: 2026-08-17

## Status

Proposed. This decision would generalize the host-testing boundary established for
NUnit by [0016](0016-nunit-native-runtime-and-mtp-first-integration.md). NUnit
remains supported, but it no longer owns shared test-platform, Runner, or host
transport concepts.

## Context

The current host-test pipeline is implemented under `DevTools.NUnit.*` even
when a responsibility is not NUnit-specific. MTP discovery, Runner process
activation, Autodesk host locate/launch/reuse, Named Pipe sessions,
cancellation, output forwarding, generation staging, and neutral result DTOs
are platform responsibilities. Keeping them under NUnit makes a second
framework either duplicate those mechanisms or depend on NUnit-named types.

`DevTools.NUnit.Runner.exe` is still a development-phase executable name, not
a published compatibility contract. Its callers and package targets are all
within the migration scope, so retaining two executable names would add a shim
without protecting a supported external dependency.

MTP is the supported northbound test platform. It should coordinate discovery
and execution without defining NUnit, xUnit, or future framework semantics.
Discovery must be safe for IDE background activity: it may inspect or load the
test application locally, but it must never locate, launch, attach to, or send
IPC to an Autodesk host. Only a run request may activate `DevTools.TestRunner`
and connect to a host.

Frameworks do not share one discovery or execution model. NUnit owns its
runtime discovery tree, xUnit owns unique IDs and message contracts, and TUnit
owns generated registrations. A lowest-common-denominator model would lose
parameterized cases, hierarchy, traits/properties, explicit behavior, or
framework extensions.

## Decision

1. **Introduce `DevTools.Testing.Abstractions`.** It contains contract-only,
   host-neutral, framework-neutral types for protocol version, framework ID,
   assembly/run identity, opaque test IDs, selections, execution options,
   session lifecycle, progress, output, attachments, diagnostics, cancellation,
   and terminal results. It contains no MTP, NUnit, xUnit, Revit, AutoCAD,
   reflection, runner-process, or serialization implementation types.

2. **Introduce `DevTools.Testing.Transport`.** It owns JSON serialization,
   versioned wire envelopes, Named Pipe mapping, and the process client that
   invokes the installed TestRunner for execution. It references
   `DevTools.Testing.Abstractions`, but neither MTP nor a test framework.

3. **Introduce `DevTools.Testing.Mtp`.** It owns reusable MTP orchestration,
   session handling, common `TestNode` property mapping, and error translation.
   It references MTP, `DevTools.Testing.Abstractions`, and
   `DevTools.Testing.Transport`. It does not discover framework attributes and
   does not interpret opaque framework IDs or payloads.

4. **Register one thin MTP framework per test framework.** `DevTools.NUnit.Mtp`
   and `DevTools.Xunit.Mtp` each register their own MTP `ITestFramework` and
   supply framework-owned discovery, identity, filter, result, and capability
   mapping. There is no universal MTP framework that guesses which provider
   owns an assembly. A host-test project selects exactly one DevTools framework
   package.

5. **Discovery is local and host-free.** A `DiscoverTestExecutionRequest` must
   complete without invoking `DevTools.TestRunner`, host discovery, host
   launch, Named Pipe connection, debugger attach, or any Autodesk process
   operation. Local framework discovery may execute framework discovery hooks
   or data providers; the product guarantees no host activation, not that
   arbitrary user discovery code has no side effects.

6. **Execution is remote and framework-routed.** A
   `RunTestExecutionRequest` sends a framework ID, assembly identity, opaque
   selection, and neutral execution options through `DevTools.TestRunner`.
   The in-host testing service selects a registered framework provider and
   emits only neutral events across the host boundary. Provider-owned framework
   objects never cross `DevTools.Testing.Abstractions`.

7. **Opaque identity is mandatory.** Shared code must not reconstruct a test
   ID from `FullyQualifiedName`, display name, method name, or source location.
   The owning provider creates and consumes the ID. Optional provider payloads
   are versioned opaque bytes or strings; the shared layer routes but never
   parses them.

8. **Split control and framework planes.** The shared control plane owns host
   selection, launch/reuse, run IDs, timeout, cancellation, progress/output,
   attachments, errors, and protocol negotiation. Each framework plane owns
   discovery, test hierarchy, selection semantics, runtime lifecycle,
   scheduling rules, and result interpretation.

9. **Rename the executable to `DevTools.TestRunner`.** It is the canonical CLI
   and deployed executable for all host-test frameworks. Commands accept an
   explicit framework ID or a provider-specific command group. The Runner may
   perform local CLI discovery but must obey the same no-host-on-discovery rule.

10. **Cut over the Runner identity directly.** Rename the project, assembly, and
   executable from `DevTools.NUnit.Runner` to `DevTools.TestRunner`, then update
   every NUnit launcher, package target, generated setting, deployment path,
   build target, and test in the same migration phase. Do not deploy a copied,
   forwarding, or compatibility `DevTools.NUnit.Runner.exe`.

11. **Use a generic host protocol with a compatibility bridge.** New endpoints
    use a versioned `testing/*` envelope and require `framework_id`. Existing
    `nunit/*` requests remain routed to the NUnit provider until the NUnit
    package and legacy VSTest surface have migrated. New framework capabilities
    must not be added to the legacy envelope.

12. **Keep host composition neutral.** `DevTools.Testing.Host` owns provider
    registration, dispatch, shared generation/session infrastructure, and
    neutral request handling. `DevTools.NUnit.Host` and
    `DevTools.Xunit.Host` register provider implementations. Shared
    `DevTools.*` projects contain no Autodesk API types; Revit and AutoCAD
    composition still own host-context dispatch.

13. **Framework runtimes are private closures.** The main add-in may ILRepack
    framework-neutral host/transport implementation according to [0019], but
    NUnit/xUnit/TUnit framework assemblies and provider implementations that
    reference them are dynamically loaded from provider-owned runtime folders.
    `DevTools.Testing.Abstractions` remains one loose shared assembly to preserve
    cross-load-context type identity.

14. **Runner owns process and debugger policy.** MTP adapters never locate,
    launch, reuse, terminate, or attach to an Autodesk process. They may start
    the installed TestRunner as a one-shot child and pass debug intent. The
    TestRunner owns host and debugger policy as established by [0016].

15. **Cancellation is cooperative.** The neutral protocol distinguishes a
    cancellation request, cancellation acknowledgement, completed cancellation,
    and a poisoned session whose in-process framework code did not stop. Shared
    code must not claim that cancelling a Task or unloading an ALC terminated a
    running test.

## Alternatives Considered

1. **Keep shared infrastructure under `DevTools.NUnit.*`.** Rejected because it
   makes every additional framework depend on NUnit naming and contracts.
2. **One universal MTP `ITestFramework`.** Rejected because framework
   capability negotiation, identity, filtering, and extension behavior would
   become ambiguous or least-common-denominator.
3. **Expose MTP types in Abstractions.** Rejected because the TestRunner and
   Autodesk host do not need MTP and would be forced to load a northbound
   platform dependency.
4. **Discover inside Revit for exact framework results.** Rejected because IDE
   background discovery must never launch or attach to a host.
5. **Keep a `DevTools.NUnit.Runner.exe` compatibility alias.** Rejected because
   the name is not yet a supported external contract and all current callers
   can move atomically. A second executable would increase packaging and
   diagnostic ambiguity without preserving required compatibility.

## Consequences

Positive:

- NUnit, xUnit, and future providers share one host control plane without
  sharing framework semantics.
- IDE discovery cannot accidentally open Revit or AutoCAD.
- Framework dependencies stay outside the main add-in image and can be staged
  as coherent provider generations.
- The generic TestRunner can evolve independently from framework packages.

Tradeoffs:

- Migration temporarily carries generic and legacy NUnit protocol envelopes,
  but only one Runner executable identity.
- Every provider still needs its own thin MTP adapter and in-host runtime.
- Full framework capability requires provider-specific tests; the neutral
  contract alone cannot prove semantic parity.
- Local discovery can still execute user discovery code even though it cannot
  activate an Autodesk host.

## Follow-Up

- After approval, create one durable execution plan from
  [`docs/templates/exec-plan.md`](../templates/exec-plan.md) under
  `docs/plans/active/` and implement the migration in independently verified
  phases.
- Update host-boundary documentation when the generic projects replace the
  NUnit-named infrastructure.
