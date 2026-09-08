# MTP Host Testing (Agent Digest)

Supported product path via NuGet `RevitDevTool.TestAdapter` (MTP-only on
`develop`). Product: `docs/product/host-testing.md`. TUnit provider:
`docs/product/tunit-host-testing.md`. Structure / release split:
`docs/architecture/Testing/README.md`. Run: `.agents/skills/revit-test/SKILL.md`.

## Verify

```powershell
dotnet build source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj -c Debug
dotnet run --project tests/DevTools.TestAdapter.Tests/DevTools.TestAdapter.Tests.csproj
dotnet run --project tests/DevTools.NUnit.MTP.Tests/DevTools.NUnit.MTP.Tests.csproj
dotnet run --project tests/DevTools.TestRunner.Tests/DevTools.TestRunner.Tests.csproj
# Live MTP (host running; scoped global.json):
cd samples/DevTools.NUnit.SampleTests
dotnet test --project DevTools.NUnit.SampleTests.csproj -c Debug.Autodesk.2026 --filter Arithmetic_runs_inside_host
# TUnit sample (same host pipe):
cd samples/DevTools.TUnit.SampleTests
dotnet test --project DevTools.TUnit.SampleTests.csproj -c Debug.Autodesk.2026 --filter Arithmetic_runs_inside_host
```

Host DLL changes: `scripts/build-host.ps1 -Year <year>`. Runner: `dotnet publish source/DevTools.TestRunner -c Release`. Adapter nupkg: `scripts/pack-test-adapter.ps1` (not `scripts/pack.ps1`).

## Pattern

- Pattern: `HostName`, `HostVersion`, `ForceLaunch`, `PerTestTimeout`, `LaunchTimeout` + NUnit (default) or TUnit (`TestingFramework=tunit`). MTP consumers add `RevitDevTool.TestAdapter`; the package copies `DevTools.NUnit.MTP.dll` or `DevTools.TUnit.MTP.dll` beside the test exe. `UseRevit`/`UseAutoCad` are this repo's sample compile flags, not package settings.
- `--filter` is the adapter method-name option (NUnit `<name re="1">` regex). `--filter-uid` is the json TestNode uid (ordinary `ITest.FullName`; `TestName`/`SetName` is `Class.Method("DisplayName")`). `--list-tests` text prints DisplayName; json uid is that TestNode uid. Do not paste a text list line as `--filter-uid`. TestRunner does not discover tests.
- MTP samples: `dotnet test` from the sample directory (scoped `global.json`). In-repo `tests/`: `dotnet run --project tests/<proj>/<proj>.csproj` (root `global.json` is not MTP `dotnet test`). Never VSTest `--filter FullyQualifiedName~`.

## Traps

- Do not use `NUnit.Engine` in the host.
- Do not add `NUnit3TestAdapter` to a host-test project.
- `samples/ricaun.NUnit.SampleTests` is third-party VSTest (`ricaun.RevitTest.TestAdapter`) against the same `HostSmokeTests`. Not the product path. Do not treat its explorer tree as a DevTools discovery bug, and do not add `NUnit3TestAdapter` or `executor://DevTools.NUnit.V1/` ignore-list workarounds. Sample `Intentional_failure_for_demo` is an expected `Assert.Fail`.
- Test Explorer refresh / `dotnet test --list-tests` must not start a host. Discovery is local NUnit `ExploreTests` and must not read `testconfig.json` host options. `ForceLaunch=false` still starts a matching-version host on **run** if none is open. The adapter reads `devtools.frameworkId`, `mtpAssembly`, and `mtpEntry` (`HostMtpRegistration` in TestAdapter). Missing keys set `LastError` and surface as a discovery/run error node — they must not `TypeInitializationException`. Override with `<MTPAssembly>` / `<MTPEntry>`. A user-authored `testconfig.json` `devtools` section without those three keys is a **build Error**. TUnit: `docs/product/tunit-host-testing.md`.
- Test Explorer launching `DevTools.TestAdapter.dll` (`hostpolicy.dll` / missing `runtimeconfig.json`): VS treated the adapter library as an MTP testhost. `Microsoft.Testing.Platform` defaults `IsTestingPlatformApplication=true` and adds `ProjectCapability TestingPlatformServer` even when `IsTestProject=false`. The adapter must set `IsTestingPlatformApplication=false`. Discover `DevTools.TUnit.SampleTests.exe` / `DevTools.NUnit.SampleTests.exe`, not `source/DevTools.TestAdapter/bin/...`. `ricaun.RevitTest.TestAdapter` scanning `Abstractions` / `Ipc` / `Transport` net48 DLLs is the VSTest sample in `Sample.slnx`, not the MTP product path.
- Test Explorer **"Test discovery aborted: 0 Tests found"**: testhost died during hook static init or Discover. Usual cause is a stale `DevTools.NUnit.MTP.dll` next to the test exe after `IHostTestDiscoverer` changed (`TypeLoadException: Method 'ToHostSelection' … does not have an implementation`). In-repo copy is `bin\Debug|Release\$(TargetFramework)\`, not `bin\Debug.Autodesk.YYYY\`. Building only `Debug.Autodesk.2024` used to leave 2022/2023/2025 testhosts with a missing or old sibling DLL. Rebuild the test project (MTP builds as Debug/Release + TFM via `SetConfiguration`). `dotnet test --list-tests` from the test project folder (scoped `global.json`) lists leaves when the sibling DLL matches. Hook register failures no longer `TypeInitializationException`; they surface as a discovery error node. Visual Studio `Sample.slnx` must **build** the MTP sample test projects (not `Build Project=false`); solution config must be an Autodesk year, not plain `Debug`.
- Autodesk configs flatten host obj/bin. MTP overrides `AppendTargetFrameworkToOutputPath=true` so its three TFMs never share a folder (CS2012 / MSB3713). Do not collapse `TargetFrameworks` on packable projects.
- Adapter pack constraints (MTP sibling, restore/TFM): `docs/architecture/Testing/README.md`.
- net48 Test Explorer "could not be discovered": `CreateTestSession` failed to load `Unsafe` 6.0. Testhost BCL comes from the adapter's `Microsoft.Testing.Platform.MSBuild` graph plus `AutoGenerateBindingRedirects`; the adapter ILRepacks its own copy and does not ship loose 3rd-party DLLs.
- MTP samples are `OutputType=Exe`. Generation snapshot must treat `.exe` as a managed test assembly and skip `Log/` / `TestResults/` / `*.diag`.
- Live `testing/run` is marshaled through `IHostContextExecutor` with NUnit `RunOnMainThread`. WPF `Dispatcher.Invoke` is not a Revit API context. Runtime unit tests keep the worker dispatcher so cancel still works.
- Stream-load leaves `Assembly.Location` empty. Tests that locate assets must use NUnit `TestContext.WorkDirectory` (the generation shadow, which copies output including Content).
- Do not add a Host `TraceListener` or `ILogger` dump of `CaseResult.Output` to “help” the pane. Trace/Debug already fan out; Console is write-through at case finish ([0017](../decisions/0017-nunit-host-test-output-routing.md)).
- `TestingRunTraceScope` (Abstractions) is IDE stdout capture only. net48 has no ALC; ALC also does not isolate `Trace.Listeners`. TUnit uses the same helper around `TUnit.Engine`.
- TUnit in-host `MissingMethodException` on `ClientInfoService`: MTP 2.4.0 requires `IClientInfo` + `IClientCapabilities`. Runtime registers `TUnitEngineClientInfo` (not the 2-arg internal ctor). Redeploy the host (Runtime ships in the installer, not the TestAdapter nupkg).
- Test Explorer **"Test discovery aborted: 0 Tests found"** after changing TUnit/NUnit catalog: testhost sibling `DevTools.*.MTP.dll` was timestamp-stale. Sibling copy must run with `SkipUnchangedFiles=false` and must not take a leftover nupkg `build/runtime` copy over the in-repo MTP bin. Rebuild the test project (not only the host year).
- Visual Studio **Debug** in Test Explorer attaches to the MTP testhost, then Runner `--debug-parent-pid` EnvDTE-attaches that VS instance to the Autodesk host ([0025](../decisions/0025-runner-owned-visual-studio-host-attach.md)). Stop Debugging while the host is still booting cancels that wait and kills only the process this run spawned — not a reused host. Cancel at a host breakpoint: Continue (or detach) before the next run; the idle thread is still in that test. Rider / C# Dev Kit: attach host PID then **Run**. VS Code/forks + PyCharm Python: `debugpy` `:5678` (`.vscode/launch.json`, `.run/Attach.run.xml`). Do not put `Microsoft.VisualStudio.Interop` on `RevitDevTool.NUnit`. Runner host year is `--host-version`, not `--version`.
