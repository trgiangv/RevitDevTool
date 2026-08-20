# NUnit Host Testing (Agent Digest)

Experimental. Product: `docs/product/nunit-host-testing.md`.
Run: `.agents/skills/revit-nunit/SKILL.md`.

## Verify

```powershell
dotnet build source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj -c Debug
scripts/test-dotnet.ps1 -Project tests/DevTools.TestAdapter.Tests/DevTools.TestAdapter.Tests.csproj
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.MTP.Tests/DevTools.NUnit.MTP.Tests.csproj
scripts/test-dotnet.ps1 -Project tests/DevTools.TestRunner.Tests/DevTools.TestRunner.Tests.csproj
# Live MTP (host running; scoped global.json):
cd samples/DevTools.NUnit.SampleTests
dotnet test --project DevTools.NUnit.SampleTests.csproj -c Debug.Autodesk.2026 --filter Arithmetic_runs_inside_host
```

Host DLL changes: `scripts/build-host.ps1 -Year <year>`. Runner: `dotnet publish source/DevTools.TestRunner -c Release`.

## Pattern

- Package contract: `HostName`, `HostVersion`, `ForceLaunch`, `PerTestTimeout`, `LaunchTimeout` + NUnit. MTP consumers add `RevitDevTool.TestAdapter`; the package copies `DevTools.NUnit.MTP.dll` beside the test exe and reuses the consumer NUnit reference. `UseRevit`/`UseAutoCad` are this repo's sample compile flags, not package settings.
- `--filter` is the adapter method-name option (NUnit `<name>` regex). `--filter-uid` is the platform UID list (`ITest.FullName`). `--list-tests` text prints DisplayName; json uid is FullName. Do not paste a text list line as `--filter-uid`. TestRunner does not discover tests.
- MTP: `dotnet test` from the sample directory (scoped `global.json`).

## Traps

- Do not use `NUnit.Engine` in the host.
- Do not add `NUnit3TestAdapter` to a host-test project.
- Rider **MTP + net48**: two `HostSmokeTests` trees (native NUnit PSI + DevTools MTP). Only the MTP/DevTools node runs in-host. **MTP + net8**: one tree (native NUnit does not claim the MTP exe). Visual Studio does not double-discover. There is **no** csproj/attribute/ExecutorUri hook to suppress native NUnit; ricaun’s 1-suite explorer is VSTest + NUnit 3, not a discovery API we are missing. Current Rider Testing Platform UI: enable MTP. The old “Ignore projects discovered by other providers” checkbox may be absent — do not block on it. **VSTest** sample: enable adapters, keep project mask `*Tests*` (ProjectReference alone is not enough). Do not add `executor://DevTools.NUnit.V1/` to the adapter ignore list. Do not add `NUnit3TestAdapter`. Sample `Intentional_failure_for_demo` is an expected `Assert.Fail`. A suite-level `ArgumentException` “same key already added” after running all MTP cases is IDE result merge, not a host failure.
- Test Explorer refresh / `dotnet test --list-tests` must not start a host. Discovery is local NUnit `ExploreTests`. `ForceLaunch=false` still starts a matching-version host on **run** if none is open.
- Autodesk configs flatten host obj/bin. MTP overrides `AppendTargetFrameworkToOutputPath=true` so its three TFMs never share a folder (CS2012 / MSB3713). Do not collapse `TargetFrameworks` on packable projects.
- net48 Test Explorer "could not be discovered": `CreateTestSession` failed to load `Unsafe` 6.0. Package props generate binding redirects and pin Unsafe 6.1.2. Adapter does not hit this because it ILRepacks.
- MTP samples are `OutputType=Exe`. Generation snapshot must treat `.exe` as a managed test assembly and skip `Log/` / `TestResults/` / `*.diag`.
- Live `nunit/run` is marshaled through `IHostContextExecutor` with NUnit `RunOnMainThread`. WPF `Dispatcher.Invoke` is not a Revit API context. Runtime unit tests keep the worker dispatcher so cancel still works.
- Stream-load leaves `Assembly.Location` empty. Tests that locate assets must use NUnit `TestContext.WorkDirectory` (the generation shadow, which copies output including Content).
- Do not add a Host `TraceListener` or `ILogger` dump of `CaseResult.Output` to “help” the pane. Trace/Debug already fan out; Console is write-through at case finish ([0017](../decisions/0017-nunit-host-test-output-routing.md)).
- `NUnitRunTraceScope` is IDE stdout capture only. net48 has no ALC; ALC also does not isolate `Trace.Listeners`.
- Visual Studio **Debug** in Test Explorer attaches to the MTP/testhost exe, then Runner `--debug-parent-pid` EnvDTE-attaches that VS instance to the Autodesk host PID (`--debug-parent-pid` implies debug). Do not put `Microsoft.VisualStudio.Interop` on `RevitDevTool.NUnit`. Runner host year is `--host-version`, not `--version`. Rider / C# Dev Kit: Attach to Process on the host PID.
