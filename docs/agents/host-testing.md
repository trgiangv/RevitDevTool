# MTP Host Testing (Agent Digest)

`RevitDevTool.TestAdapter` (NuGet) is the supported test integration. NUnit is
the default engine; TUnit and MSTest are opt-in (`TestingFramework` `tunit` or
`mstest`).

- Product: `docs/product/host-testing.md`, `docs/product/tunit-host-testing.md`.
  Accepted framework versions: `docs/product/host-testing.md#framework-versions`.
- Structure: `docs/architecture/Testing/README.md`.
- Writing and running tests: `.agents/skills/revit-test/SKILL.md`.
- Consumer setup: `source/DevTools.TestAdapter/README.md`.

## Main flow

```text
test csproj (HostName, HostVersion, NUnit|TUnit|MSTest)
  -> package targets: Reference the selected DevTools.{NUnit|TUnit|MSTest}.MTP.dll
     + Abstractions, copy the framework runtime + manifest to the output,
     write discovery-refs.txt, compile [assembly: AssemblyMetadata("devtools.*")]
  -> generated MTP entry point calls adapter hook + selected sibling hook
  -> discovery: local ExploreTests / TUnit catalog / MSTest --list-tests, no host process
  -> run: TestRunner.exe locates or launches the host, then testing/hello|run|cancel
```

Host options, `devtools.frameworkId` included, are assembly metadata in the test
assembly. A hand-written `testconfig.json` `devtools` key overrides that one key
and needs no others. The sibling is chosen in MSBuild (`TestingFramework` gives
one `Reference` and its builder hook), not by runtime plugin keys.

## Verify

```powershell
# 1. compile + in-repo tests (adapter unit tests still ProjectReference)
dotnet build source/DevTools.Testing.Host/DevTools.Testing.Host.csproj -c Debug
dotnet run --project tests/DevTools.TestAdapter.Tests/DevTools.TestAdapter.Tests.csproj
dotnet run --project tests/DevTools.NUnit.MTP.Tests/DevTools.NUnit.MTP.Tests.csproj
# TestRunner.Tests is an MSTest.Sdk MTP testhost
dotnet run --project tests/DevTools.TestRunner.Tests/DevTools.TestRunner.Tests.csproj

# 2. pack a 0.0.0-local.<stamp> nupkg to output/nettest (samples float on it)
dotnet run --project build -- pack nettest

# 3. package surface (restore from that nupkg + host-free discovery), net48 / net8 / net10
scripts/test-adapter-matrix.ps1

# 4. live (host running; root global.json is MTP)
# Normal output hides passed blocks — use Detailed to see Console/Trace on the terminal.
dotnet test --project samples/DevTools.NUnit.SampleTests/DevTools.NUnit.SampleTests.csproj -c Debug.Autodesk.2026 --filter Arithmetic_runs_inside_host --output Detailed
dotnet test --project samples/DevTools.NUnit.SampleTests/DevTools.NUnit.SampleTests.csproj -c Debug.Autodesk.2024 --filter Writes_output --output Detailed -v q
```

What to rebuild:

- Host DLL change: `scripts/build-host.ps1 -HostApp Revit -Year <year>`. Several
  hosts or years: [build-matrix.md](build-matrix.md#host-build-script).
- Adapter, runner, or framework runtime change: `dotnet run --project build -- pack nettest`,
  then rebuild the test project. The pack publishes `DevTools.TestRunner` into the
  package (`tools/runner`) and samples run that copy from the restored package;
  nothing is deployed to the bundle `Contents` or the Velopack folder, and there is
  no runner-path override. Each pack has a new `0.0.0-local.<stamp>` version and
  samples restore with `RestoreForce=true`, so no stale extraction is reused.
- Installer: `dotnet run --project build -- pack addin`.

## Rules

- Consumer properties: `HostName`, `HostVersion`, `ForceLaunch`, `PerTestTimeout`,
  `LaunchTimeout`, `TestingFramework` (`nunit` default, `tunit` or `mstest` opt-in).
  An `mstest` project is `Sdk="MSTest.Sdk"` (or `EnableMSTestRunner=true`).
- Leave `RuntimeIdentifier` unset on net8 and net10 test projects: a RID on net8
  (Revit 2025) nests testhost output under `win-x64`. The package sets
  `AppendRuntimeIdentifierToOutputPath=false`; samples set `win-x64` only when
  `TargetFramework` starts with `net4`.
- `net48` + TUnit needs no `Polyfill`. The package defaults `EnableTUnitPolyfills=false`
  and compiles `build/netfx/ModuleInitializerAttribute.cs` into the project, skipping it
  when a `Polyfill` reference or another `ModuleInitializerAttribute.cs` is already
  there. Set `NetFxModuleInitializer=false` only when that skip misses an
  existing type (see `docs/product/tunit-host-testing.md`).
- The packaged `build/*.props|targets` read nothing from `$(Configuration)`, repo
  paths, or `ProjectReference`, because consumers have none of them. In-repo samples
  restore `RevitDevTool.TestAdapter` from `output/nettest`, the same nupkg an end
  user gets; there is no checkout-only `Local.targets` path.
- `--filter` is the method-name option (NUnit `<name re="1">` regex). `--filter-uid` is
  the TestNode uid (`ITest.FullName`; for `TestName`/`SetName` it is
  `Class.Method("DisplayName")`). `--list-tests` text prints the DisplayName, so take
  uids from `--list-tests json`. TestRunner does not discover tests.
- Run MTP samples with `dotnet test --project samples/DevTools.*.SampleTests/…` from
  the repo root (root `global.json` is MTP). In-repo `tests/` run with
  `dotnet run --project tests/<proj>/<proj>.csproj` or `dotnet test --project`
  from the root. `samples/ricaun.NUnit.SampleTests` overrides to VSTest; `cd` into
  that folder. VSTest `--filter FullyQualifiedName~` does not work on MTP projects.
- Build samples with an Autodesk configuration (`Debug.Autodesk.2025`, …). Plain
  `Debug` sets no `RevitVersion` or `TargetFramework`, so the sample does not build
  and Test Explorer falls back to a source tree that is not MTP discovery.
- Autodesk configs flatten host obj/bin. MTP keeps `AppendTargetFrameworkToOutputPath=true`
  so its three TFMs never share a folder (CS2012 / MSB3713); keep `TargetFrameworks`
  multi-valued on packable projects.

## Traps

- Discovery stays host-free: Test Explorer refresh and `--list-tests` start no host
  and read no `testconfig.json` host options. `ForceLaunch=false` still starts a
  matching-version host on run when none is open.
- "Test discovery aborted: 0 Tests found" means the testhost died in static init or
  Discover. The usual cause is a timestamp-stale `DevTools.*.MTP.dll` beside the test
  exe (`TypeLoadException` on an `ITestDiscoverer` member). Rebuild the test project,
  not only the host year; the sibling copy runs with `SkipUnchangedFiles=false`.
- The host uses its own NUnit runtime, so keep `NUnit.Engine` out of the host and
  `NUnit3TestAdapter` out of host-test projects. `samples/ricaun.NUnit.SampleTests`
  is the third-party VSTest comparison over the same `HostSmokeTests`; its explorer
  tree is not a DevTools discovery bug, and `Intentional_failure_for_demo` fails on
  purpose.
- net48 "could not be discovered" means the testhost failed to bind `Unsafe` 6.0. That
  BCL comes from the adapter's `Microsoft.Testing.Platform.MSBuild` graph plus the
  binding redirects written by the SDK default `AutoGenerateBindingRedirects=true`;
  the nupkg ships no loose third-party DLLs and adds no redirect target. Keep that
  default on net48 test projects.
- net48 has no load context: if the host already loaded an assembly with the same
  identity, in-host tests bind that copy, not the generation snapshot. Restart the
  host after deploying, or use net8+.
- Stream-loaded assemblies have an empty `Assembly.Location`, and NUnit
  `TestContext.WorkDirectory` and TUnit work dirs point at a generation shadow. Locate
  assets with `[CallerFilePath]` (see the `revit-test` skill).
- Live `testing/run` is marshaled through `IHostContextExecutor` (NUnit
  `RunOnMainThread`). WPF `Dispatcher.Invoke` is not a Revit API context.
- Trace and Debug already fan out and Console is write-through at case finish
  ([0017](../decisions/0017-nunit-host-test-output-routing.md)), so a Host
  `TraceListener` or `ILogger` dump of `CaseResult.Output` would duplicate output.
- TUnit in-host `MissingMethodException` on `ClientInfoService`: MTP needs
  `IClientInfo` and `IClientCapabilities`; the Runtime registers `TUnitEngineClientInfo`
  and ships in the nupkg, so pack the adapter and rebuild the test project (not the host).
- Visual Studio Debug attaches the testhost, then the Runner EnvDTE-attaches that VS
  instance to the host ([0025](../decisions/0025-runner-owned-visual-studio-host-attach.md)).
  Stop Debugging during launch kills only a host this run spawned. Rider and C# Dev
  Kit: attach the host PID, then Run. The Runner host year is `--host-version`.
