# Decision 0022: NUnit Uses An MTP-Only Testing Stack

Date: 2026-08-18

## Status

Accepted

## Context

Decision 0021 separated the neutral testing kernel from NUnit and preserved the
existing VSTest adapter and legacy `nunit/*` protocol as compatibility surfaces.
That compatibility baseline is retained on branch `testing/nunit-vstest` at
commit `e32ae590`.

On `develop`, maintaining VSTest and MTP together keeps a second discovery and
execution stack, requires `netstandard2.0` adapter targets, exposes additional
test-platform assemblies to IDE discovery, and preserves provider-specific
wire contracts that are unnecessary for MTP execution.

## Decision

`develop` supports NUnit host testing through Microsoft Testing Platform only.

1. Delete the NUnit VSTest adapter, its tests, build imports, samples, solution
   entries, packaging inputs, and documentation.
2. Remove repository-wide VSTest runner dependencies:
   `Microsoft.NET.Test.Sdk`, `Microsoft.TestPlatform.ObjectModel`,
   `xunit.runner.visualstudio`, `NUnit3TestAdapter`, and
   `ricaun.RevitTest.TestAdapter`. Repository xUnit v3 tests run as MTP
   executables.
3. Preserve `NUnit` itself: it remains the provider framework executed inside
   the host. Preserve `Microsoft.Testing.Platform` and
   `Microsoft.Testing.Platform.MSBuild` where required.
4. Delete the legacy `nunit/*` bridge protocol, compatibility DTOs, legacy pipe
   client, and legacy CLI mode. TestRunner invokes only the neutral
   `testing/hello`, `testing/run`, and `testing/cancel` surface.
5. Delete `DevTools.NUnit.Transport`. NUnit Runtime implements
   `ITestingRuntimeSession` directly; the only loose cross-load-context
   contract is `DevTools.Testing.Abstractions`.
6. Remove `netstandard2.0` targets and compatibility branches that existed for
   the VSTest adapter. Keep `net48`, net8, and net10 host support.
7. Discovery remains local metadata discovery and never launches or contacts
   Revit/AutoCAD. Host activation remains execution-only.

## Dependency Direction

```text
NUnit test project
  -> RevitDevTool.NUnit (MTP framework)
  -> DevTools.Testing.Mtp / Testing.Transport
  -> DevTools.TestRunner
       -> DevTools.TestRunner.Core
       -> DevTools.NUnit.Runner / NUnit.Discovery
  -> testing/* host bridge
  -> DevTools.Testing.Host
  -> DevTools.NUnit.Host policy + DevTools.NUnit.Runtime
  -> DevTools.Testing.Abstractions (single loose runtime identity)
```

No VSTest adapter, NUnit-specific bridge contract, or NUnit transport assembly
participates in this graph.

## Consequences

- IDE discovery has one test-platform integration path.
- The published MTP package has a smaller private closure and no VSTest or
  `netstandard2.0` compatibility payload.
- Legacy VSTest consumers must use branch `testing/nunit-vstest` or migrate to
  `RevitDevTool.NUnit` MTP projects.
- `net48` remains supported for older Autodesk hosts; only the adapter-oriented
  `netstandard2.0` target is removed.
- PolySharp and ILRepack policy remain outside this decision. Warning/noise
  evaluation follows after the MTP-only graph is stable.

## Validation

- repository architecture scans find no VSTest adapter project, legacy
  `nunit/*` protocol, VSTest package, `netstandard2.0` target, or NUnit
  Transport assembly;
- all xUnit repository test projects build and execute with their MTP entry
  points and no VSTest runner packages;
- NUnit MTP clean-consumer restore, discovery, and run bootstrap pass for
  net48/net8/net10;
- local discovery proof shows zero host activation;
- Host/Runtime tests prove the neutral runtime contract, cancellation,
  generation isolation, and result capability parity;
- Revit/AutoCAD host projects compile without deployment or host launch.
