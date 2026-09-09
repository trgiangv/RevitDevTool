# MTP Host Testing (Agent Digest)

`RevitDevTool.TestAdapter` (NuGet) is the only supported test integration. NUnit is
the default engine, TUnit is opt-in. Product: `docs/product/host-testing.md` ·
`docs/product/tunit-host-testing.md`. Structure: `docs/architecture/Testing/README.md`.
Run/author tests: `.agents/skills/revit-test/SKILL.md`.

## Main flow

```text
test csproj (HostName, HostVersion, NUnit|TUnit)
  -> package targets: copy DevTools.{NUnit|TUnit}.MTP.dll + Abstractions beside the exe,
     write discovery-refs.txt and [AssemblyName].testconfig.json
  -> discovery: local ExploreTests / TUnit catalog in the testhost, no host process
  -> run: TestRunner.exe locates or launches the host, then testing/hello|run|cancel
```

Adapter reads `devtools.frameworkId`, `mtpAssembly`, `mtpEntry` from `testconfig.json`
(`HostMtpRegistration`). Missing keys set `LastError` and surface as an error node —
they must never throw from the hook static constructor. Override with `<MTPAssembly>` /
`<MTPEntry>`. A hand-written `testconfig.json` `devtools` section without all three
keys is a build Error.

## Verify

```powershell
# 1. compile + in-repo tests
dotnet build source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj -c Debug
dotnet run --project tests/DevTools.TestAdapter.Tests/DevTools.TestAdapter.Tests.csproj
dotnet run --project tests/DevTools.NUnit.MTP.Tests/DevTools.NUnit.MTP.Tests.csproj
dotnet run --project tests/DevTools.TestRunner.Tests/DevTools.TestRunner.Tests.csproj

# 2. package surface (build + host-free discovery), net48 / net8 / net10
scripts/pack-test-adapter.ps1 -RefreshLocalCache
scripts/test-adapter-matrix.ps1

# 3. live (host running; root global.json is MTP)
dotnet test --project samples/DevTools.NUnit.SampleTests/DevTools.NUnit.SampleTests.csproj -c Debug.Autodesk.2026 --filter Arithmetic_runs_inside_host
```

Host DLL changes: `scripts/build-host.ps1 -Year <year>`. Runner:
`dotnet publish source/DevTools.TestRunner -c Release`. Adapter nupkg:
`scripts/pack-test-adapter.ps1` (not `scripts/pack.ps1`).

## Rules

- Consumer properties: `HostName`, `HostVersion`, `ForceLaunch`, `PerTestTimeout`,
  `LaunchTimeout`, `TestingFramework` (`nunit` default, `tunit` opt-in). `UseRevit` /
  `UseAutoCad` are this repo's sample compile flags, not package settings.
- `net48` consumers add `<RuntimeIdentifier>win-x64</RuntimeIdentifier>` (`NETSDK1047`,
  restore does not read a RID from `build/*.props`). That is the only consumer-side
  setting the package cannot supply.
- `net48` + TUnit needs no `Polyfill`. The package defaults `EnableTUnitPolyfills=false`
  and compiles `build/netfx/ModuleInitializerAttribute.cs` into the project, skipping it
  when a `Polyfill` reference or another `ModuleInitializerAttribute.cs` is already
  there. Opt out with `NetFxModuleInitializer=false` only when that skip misses the
  existing type (see `docs/product/tunit-host-testing.md`).
- The packaged `build/*.props|targets` must not read `$(Configuration)`, repo paths, or
  `ProjectReference`. Dev-loop MSBuild goes in
  `source/DevTools.TestAdapter/build/RevitDevTool.TestAdapter.Local.targets` (not packed).
- In-repo samples use `ProjectReference`, where the adapter's private
  `Microsoft.Testing.Platform` does not flow: the NUnit samples declare that
  `PackageReference` themselves (`CS0234` on the generated entry point otherwise).
- `--filter` is the method-name option (NUnit `<name re="1">` regex). `--filter-uid` is
  the TestNode uid (`ITest.FullName`; `TestName`/`SetName` is `Class.Method("DisplayName")`).
  `--list-tests` text prints DisplayName — never paste a text line as `--filter-uid`.
  TestRunner does not discover tests.
- MTP samples: `dotnet test --project samples/DevTools.*.SampleTests/…` from
  the repo root (root `global.json` is MTP). `samples/ricaun.NUnit.SampleTests`
  overrides to VSTest — `cd` into that folder. In-repo `tests/`:
  `dotnet run --project tests/<proj>/<proj>.csproj` still works; `dotnet test
  --project tests/<proj>/<proj>.csproj` from root is now MTP too. Never VSTest
  `--filter FullyQualifiedName~` on MTP projects.
- Use an Autodesk configuration (`Debug.Autodesk.2025`, …). Plain `Debug` does not set
  `RevitVersion` / `TargetFramework`, so the sample does not build and Test Explorer
  falls back to a source tree that is not MTP discovery.
- Autodesk configs flatten host obj/bin; MTP keeps `AppendTargetFrameworkToOutputPath=true`
  so its three TFMs never share a folder (CS2012 / MSB3713). Do not collapse
  `TargetFrameworks` on packable projects.

## Traps

- Discovery must stay host-free: Test Explorer refresh and `--list-tests` must not start a
  host, and must not read `testconfig.json` host options. `ForceLaunch=false` still starts
  a matching-version host on **run** when none is open.
- "Test discovery aborted: 0 Tests found" = the testhost died in static init or Discover.
  Usual cause is a timestamp-stale `DevTools.*.MTP.dll` beside the test exe
  (`TypeLoadException` on an `IHostTestDiscoverer` member). Rebuild the **test project**,
  not only the host year; the sibling copy runs with `SkipUnchangedFiles=false`.
- Do not use `NUnit.Engine` in the host, and never add `NUnit3TestAdapter` to a host-test
  project. `samples/ricaun.NUnit.SampleTests` is the third-party VSTest comparison sample
  over the same `HostSmokeTests`; its explorer tree is not a DevTools discovery bug.
  `Intentional_failure_for_demo` is an expected `Assert.Fail`.
- net48 "could not be discovered" = testhost failed to bind `Unsafe` 6.0. That BCL comes
  from the adapter's `Microsoft.Testing.Platform.MSBuild` graph plus
  `AutoGenerateBindingRedirects`; the nupkg ships no loose 3rd-party DLLs.
- net48 has no load context: if the host already loaded an assembly with the same identity,
  in-host tests bind that copy, not the generation snapshot. Restart the host after
  deploying, or use net8+.
- Stream-loaded assemblies have an empty `Assembly.Location`. Tests that locate assets use
  NUnit `TestContext.WorkDirectory` (the generation shadow).
- Live `testing/run` is marshaled through `IHostContextExecutor` (NUnit `RunOnMainThread`).
  WPF `Dispatcher.Invoke` is not a Revit API context.
- Do not add a Host `TraceListener` or `ILogger` dump of `CaseResult.Output`; Trace/Debug
  already fan out and Console is write-through at case finish
  ([0017](../decisions/0017-nunit-host-test-output-routing.md)).
- TUnit in-host `MissingMethodException` on `ClientInfoService`: MTP 2.4.0 needs
  `IClientInfo` + `IClientCapabilities`; the Runtime registers `TUnitEngineClientInfo` and
  ships in the installer, so redeploy the host (not the nupkg).
- Visual Studio **Debug** attaches the testhost, then the Runner EnvDTE-attaches that VS
  instance to the host ([0025](../decisions/0025-runner-owned-visual-studio-host-attach.md)).
  Stop Debugging during launch kills only a host this run spawned. Rider / C# Dev Kit:
  attach the host PID, then **Run**. Runner host year is `--host-version`.
