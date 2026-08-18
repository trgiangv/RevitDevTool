# Host testing

RevitDevTool runs tests inside Revit, AutoCAD, and Civil 3D through
Microsoft.Testing.Platform. `RevitDevTool.TestAdapter` is the only public test
integration package. The VSTest adapter and NUnit-specific bridge protocol are
not part of the supported product on `develop`; their final baseline is retained
on branch `testing/nunit-vstest`.

## User contract

- Test projects reference `RevitDevTool.TestAdapter` and
  `Microsoft.Testing.Platform.MSBuild`. The test framework package (default
  NUnit 4.6.1) is a local project choice, not a package dependency.
- Test projects are executable Microsoft.Testing.Platform applications
  (`OutputType=Exe`) and declare `HostName`, `HostVersion`, and optional
  `ForceLaunch`, `PerTestTimeout`, and `LaunchTimeout`.
  `ForceLaunch=true` always starts a new host (skip reuse).
  `PerTestTimeout` is the per-test budget after the host is ready;
  the `testing/run` pipe wait is this times the number of tests in that run.
  `LaunchTimeout` is the wait for the host pipe after process start.
- Override `TestingFramework` and `TestingDiscoveryAttributes` in the test
  csproj to change the in-host engine without changing the NuGet. Default
  engine is NUnit (`TestingFramework=nunit`).
- Host options are generated as `testconfig.json` from the csproj properties.
  An incremental build refreshes `[AssemblyName].testconfig.json`; Rebuild is
  not required after changing `HostName`, `ForceLaunch`, `PerTestTimeout`, or
  `LaunchTimeout`. Microsoft.Testing.Platform.MSBuild copies the same file.
  The adapter reads the `devtools` section
  through MTP `IConfiguration` (same pattern as `mstest` / `xUnit`). Author
  `testconfig.json` beside the csproj to add `platformOptions`; do not use
  `.runsettings`.
- IDE discovery reads PE metadata locally. It must not locate, launch, or contact
  an Autodesk host.
- Running a test starts or reuses the selected host and sends only the neutral
  `testing/hello`, `testing/run`, and `testing/cancel` contracts.
- NUnit does not own the protocol. IDE-facing types are platform `TestNode`;
  host-facing types are `testing/*`. NUnit is the default execution engine:
  in-host `nunit.framework`, and filter XML at the NUnit.Host boundary.

## Ownership

| Module | Responsibility |
|---|---|
| `DevTools.Testing.Abstractions` | Neutral run/result/runtime contracts shared across host boundaries |
| `DevTools.Testing.Transport` | `testing/*` JSON, pipe methods, and TestRunner process client |
| `DevTools.Testing.Host` | In-host `testing/*` handler, generation store, and runtime-session lifecycle |
| `DevTools.TestAdapter` | Published `RevitDevTool.TestAdapter`. Local PE metadata scan (attribute names from the test project) plus the Microsoft.Testing.Platform adapter |
| `DevTools.NUnit.Runtime` | Default in-host engine: NUnit execution inside an isolated generation |
| `DevTools.NUnit.Host` | NUnit closure/version policy, Dynamo-safe framework sharing, isolated runtime activation, and `TestingSelection` → NUnit filter XML |
| `DevTools.TestRunner.Core` | Framework-neutral host locate/launch/reuse, debugger attach, and `testing/*` pipe client |
| `DevTools.TestRunner` | Southbound executable: locate/launch the host and send `testing/run`. Framework id is a CLI option from the adapter `devtools` section |

The cross-load-context identity is `DevTools.Testing.Abstractions`. Runtime
payloads do not carry a provider-specific transport assembly. Supported runtime
targets are net48, net8, and net10; the former `netstandard2.0` compatibility
target is removed.

## Sample and execution

The maintained samples are:

- `samples/DevTools.NUnit.SampleTests` for Revit;
- `samples/DevTools.NUnit.Civil3D.SampleTests` for Civil 3D.

Those samples still use NUnit attributes because NUnit is the default engine.
`samples/ricaun.NUnit.SampleTests` is a comparison sample: it links the same
`HostSmokeTests` and runs them through `ricaun.RevitTest.TestAdapter`. It is
not the product contract.

Run the generated test executable or use the Microsoft.Testing.Platform
`dotnet test`/IDE surface provided by the installed SDK. Discovery remains
host-free; host launch occurs only after an execution request.

## Packaging

`RevitDevTool.TestAdapter` is packed from `source/DevTools.TestAdapter`. Modern
targets keep implementation assemblies in a private runtime closure; net48 keeps
the established single-assembly repack. Consumers see only the platform adapter
compile surface. The release workflow publishes this one package.

See [decision 0022](../decisions/0022-nunit-mtp-only-testing-stack.md) for the
platform-only boundary and [decision 0021](../decisions/0021-testing-kernel-and-provider-owned-framework-runtime.md)
for the neutral kernel extraction history.
