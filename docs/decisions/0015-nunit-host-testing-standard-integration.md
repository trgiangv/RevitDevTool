# 0015 NUnit Host Testing Through Standard .NET Test Integrations

Date: 2026-08-10  
Amended: 2026-08-12

## Status

Partially superseded (2026-08-12) by
[`0016-nunit-native-runtime-and-mtp-first-integration.md`](0016-nunit-native-runtime-and-mtp-first-integration.md).

The pipe, host-context execution, host-neutral project boundaries, and single
public consumer-package direction remain valid. The reflective NUnit execution
strategy, VSTest-first priority, and deferred-debugging policy are historical.

## Context

RevitDevTool and AcadDevTool already execute pytest inside a live host through
the shared `DevToolsPipeServer` and `IHostContextExecutor`. The product needs
NUnit host tests selectable from a CLI and from VSTest (`dotnet test` / Test
Explorer).

An earlier revision required public `NUnit.Engine` in-process. Live Revit .NET
Framework hosts (for example Revit 2024 with Dynamo) already load a different
`nunit.framework` identity; Engine `FrameworkController` then fails with
`FileLoadException` (`0x80131040`). DevTools therefore uses a reflective
in-host runner. Probe DLLs are **shadow-copied** to a stamp-keyed temp path and
loaded with `Assembly.LoadFile` from the copy (no bin lock; reload after
rebuild). That differs from loading the bin path directly (better for debugger
symbol binding, worse for file locks).

Comparable third-party tooling (folder zip-to-temp then `LoadFile`, external
controller + named pipe) informed the *shape* of discover/run-in-host, but
DevTools does not depend on those packages and owns transport, DI, stamp-keyed
per-file shadow, and multi-host wiring.

Product overview, coexistence notes, NuGet status, and gaps:
`docs/product/nunit-host-testing.md`.

## Decision

1. **In-host discovery and execution** use `NUnitReflectionRunner`: shadow-copy
   + `LoadFile` for the test assembly and beside-output `nunit.framework` via
   `DirectoryAssemblyLoad`, scoped `AssemblyResolve`, and attribute-name
   reflection for `[Test]` / `[TestCase]` / SetUp lifecycle. The host does
   **not** reference or deploy `NUnit.Engine`.
2. Host execution remains in-process through `IHostContextExecutor`. Deploy-folder
   loading stays on `AssemblyLoader` (LoadFrom/ALC) — never shadow/byte-load
   MahApps/UI from deploy. Test-directory probing never uses the deploy loader.
3. Reuse `BridgeMessage` transport and multiplexed `IBridgeRequestHandler`.
   NUnit adds versioned `nunit/*` contracts only (not pytest payloads).
4. Intended public package: **one** `DevTools.NUnit.TestAdapter` NuGet. **Not
   published yet** (experimental). Core, Runner, and Host are not supported
   consumer APIs.
5. VSTest adapter is a proxy to the installed Runner; it never runs host API
   tests inside `testhost`.
6. VSTest / `dotnet test` is the first IDE surface. MTP is later scope.
7. Rider is a VSTest-proxy compatibility gate only; no Rider SDK in Core/Runner/Host.
8. **Host-process debugging is deferred** for this experimental phase. CLI
   `--debug` is rejected; VSTest debug intent is not forwarded. Revisit later
   (for example debug-mode load-from-bin vs shadow).
9. Shared `DevTools.NUnit.*` projects contain no Revit or AutoCAD API types.
10. Test projects must copy a matching `nunit.framework.dll` beside the test
    assembly; host pins major.minor.

## Alternatives Considered

1. **Use `NUnit3TestAdapter` alone.** No contract into a live Revit/AutoCAD
   process.
2. **Depend on a third-party host-test package wholesale.** Rejected: keep
   DevTools pipe/DI/hosts; reuse only the reflective isolated-load *idea*.
3. **Keep `NUnit.Engine` in-process.** Fails when another `nunit.framework` is
   already loaded (Dynamo).
4. **Publish multiple NuGets per host/IDE.** Rejected: one adapter package when
   publishing begins.
5. **Ship host-process debugging in v1.** Deferred: attach/symbol binding
   conflicts with shadow-copy Location; needs a dedicated design.

## Consequences

Positive:

- Coexists with host-loaded `nunit.framework` (Dynamo).
- Stable pipe / VSTest / Runner contracts.
- Probe DLLs not file-locked; stamp reload without restarting Revit for IL.

Tradeoffs / gaps:

- Attribute subset only; no Engine feature parity.
- Debugging deferred; NuGet unpublished; MTP and broad host CI incomplete
  (see product doc Gaps).

## Follow-Up

- Publish `DevTools.NUnit.TestAdapter` when experimental gate clears.
- Reintroduce debugging with an explicit load-mode policy.
- Expand attributes and CI host matrix as product tests require.
