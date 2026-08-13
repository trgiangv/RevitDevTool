# NUnit Host Testing (Agent Digest)

Experimental. Product: `docs/product/nunit-host-testing.md`.

## Verify

```powershell
dotnet build source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj -c Debug
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Mtp.Tests/DevTools.NUnit.Mtp.Tests.csproj
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Runner.Tests/DevTools.NUnit.Runner.Tests.csproj
# Live MTP (host running; scoped global.json):
cd samples/DevTools.NUnit.SampleTests
dotnet test --project DevTools.NUnit.SampleTests.csproj -c Debug.Autodesk.2026 --filter Arithmetic_runs_inside_host
# Live VSTest (no MTP global.json):
cd samples/DevTools.NUnit.VSTest.SampleTests
dotnet test DevTools.NUnit.VSTest.SampleTests.csproj -c Debug.Autodesk.2026 --filter FullyQualifiedName~Arithmetic_runs_inside_host
```

Host DLL changes: `scripts/build-host.ps1 -Year <year>`. Runner: `dotnet publish source/DevTools.NUnit.Runner -c Release`.

## Pattern

- Package contract: `HostName`, `HostVersion`, `HostLaunch`, timeouts + NUnit. MTP consumers add `DevTools.NUnit`; VSTest consumers add `DevTools.NUnit.TestAdapter` + `Microsoft.NET.Test.Sdk`. `UseRevit`/`UseAutoCad` are this repo's sample compile flags, not package settings.
- MTP `--filter` = NUnit method name. VSTest `--filter` = `FullyQualifiedName~…`. Runner owns NUnit XML (`--name` / `--test`).
- MTP: `dotnet test` from the sample directory (scoped `global.json`). VSTest: run from that sample directory or repo root — never from an MTP sample folder.
- Four samples: MTP×Revit, MTP×Civil3D, VSTest×Revit, VSTest×Civil3D. Do not mix adapters on one project.

## Traps

- Do not use `NUnit.Engine` in the host.
- Do not add `NUnit3TestAdapter` or `NUnit.Microsoft.Testing.Platform` to a host-test project.
- Rider always shows **two** `HostSmokeTests` trees (native NUnit + DevTools). Only the second/adapter node runs in-host. Proven settings: **Testing Platform** → enable, **uncheck** “Ignore projects discovered by other providers” (else MTP is invisible because `[Test]` is already claimed). **VSTest** → enable adapters, keep project mask `*Tests*` (ProjectReference to our adapter is not enough for Rider to show the VSTest suite). Do not add `executor://DevTools.NUnit.V1/` to the adapter ignore list. Do not add `NUnit3TestAdapter`. Visual Studio does not double-discover this way.
- Test Explorer refresh must not start a host. Discovery is local PE metadata. `HostLaunch=false` still starts a matching-version host on **run** if none is open.
- Autodesk configs flatten host obj/bin. MTP overrides `AppendTargetFrameworkToOutputPath=true` so its three TFMs never share a folder (CS2012 / MSB3713). Do not collapse `TargetFrameworks` on packable projects.
- net48 Test Explorer "could not be discovered": `CreateTestSession` failed to load `Unsafe` 6.0. Package props generate binding redirects and pin Unsafe 6.1.2. Adapter does not hit this because it ILRepacks.
- MTP samples are `OutputType=Exe`. Generation snapshot must treat `.exe` as a managed test assembly and skip `Log/` / `TestResults/` / `*.diag`.
- Live `nunit/run` is marshaled through `IHostContextExecutor` with NUnit `RunOnMainThread`. WPF `Dispatcher.Invoke` is not a Revit API context. Runtime unit tests keep the worker dispatcher so cancel still works.
- Stream-load leaves `Assembly.Location` empty. Tests that locate assets must use NUnit `TestContext.WorkDirectory` (the generation shadow, which copies output including Content).
