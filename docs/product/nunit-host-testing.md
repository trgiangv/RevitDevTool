# NUnit Host Testing

Experimental in-host NUnit for Revit / AutoCAD-family via DevTools Named Pipe,
with MTP (`RevitDevTool.NUnit`) and VSTest (`DevTools.NUnit.TestAdapter`) consumers.
The MTP package requires
[RevitDevTool](https://github.com/trgiangv/RevitDevTool); the host-test
controller ships in that installer. The live CAD host executes the tests.

## Status

**Experimental.** Discover / run / progress work end-to-end. `RevitDevTool.NUnit`
(MTP) is published independently of the RevitDevTool installer. Version is
`<Version>` in `source/DevTools.NUnit.Mtp/DevTools.NUnit.Mtp.csproj` (starts at
`0.0.1`). Run `.github/workflows/PublishNUnit.yml` from GitHub Actions to pack
and push nuget.org (OIDC Trusted Publishing). VSTest stays in-tree and is not
published.

Host-process debugging is **out of scope**. There is no `--debug` CLI, no Test
Explorer Debug attach, and no IDE SDK. Attach the IDE debugger to the host
PID yourself if needed; that is not a product feature.

Two consumer surfaces share the same Runner/Host: MTP (`RevitDevTool.NUnit`) and
VSTest (`DevTools.NUnit.TestAdapter`). Keep them on **separate** test projects.

## Modules

| Project | Role |
|---------|------|
| `DevTools.NUnit.Core` | `nunit/*` wire contracts, timing, protocol version |
| `DevTools.NUnit.Host` | Native NUnit runtime in the CAD host |
| `DevTools.NUnit.Runner` | CLI controller: find/launch host pipe, discover/run |
| `DevTools.NUnit.Mtp` | MTP framework (`PackageId=RevitDevTool.NUnit`); proxies to Runner |
| `DevTools.NUnit.TestAdapter` | VSTest adapter; same Runner/Host contract |

`RevitDevTool.NUnit` is an independent test-platform package. Consumers set
`HostName` / `HostVersion` / launch timeouts; the package does not read this
repo's `UseRevit` / `UseAutoCad`. The MTP assembly itself never loads into the
CAD host. On **net48**, package targets ILRepack the consumer **test exe**
(`/internalize /union /illink`) and delete merged DLLs so the host generation
does not copy BCL polyfills (`System.Text.Json`, `Unsafe`, product libraries,
…). `nunit.framework` 4.6.1 stays loose beside the exe. Autodesk APIs and
host-shared WPF (`MahApps`, `ControlzEx`, `Xaml.Behaviors`) are not merged.
Disable with `DevToolsNUnitRepack=false`. Do not use add-in `IsRepackable` for
this. net8+ / 2027 test TFMs skip ILRepack (ALC / isolated context).
`Microsoft.Testing.Platform` is compile-only
(`PrivateAssets=all` + `ExcludeAssets=runtime`); consumers supply it via
`Microsoft.Testing.Platform.MSBuild`. Adapter ILRepack isolates VSTest
testhost, not Revit.

Samples declare the host-run contract (`HostName`, `HostVersion`, `HostLaunch`,
`HostTimeout`, `HostLaunchTimeout`). MTP writes those to `devtools.nunit.host.json`
beside the test exe; VSTest writes a generated `.runsettings` (obj path plus
`DevTools.NUnit.runsettings` beside the test DLL so IDE discovery does not need
MSBuild `RunSettingsFilePath`). Props still supply fallbacks if a property is omitted.

Four live samples — two adapters × two hosts. Do not mix MTP and VSTest on one project.

| Sample | Adapter | Host |
|--------|---------|------|
| `samples/DevTools.NUnit.SampleTests` | MTP (`RevitDevTool.NUnit`) | Revit |
| `samples/DevTools.NUnit.Civil3D.SampleTests` | MTP | Civil 3D |
| `samples/DevTools.NUnit.VSTest.SampleTests` | VSTest (`DevTools.NUnit.TestAdapter`) | Revit |
| `samples/DevTools.NUnit.VSTest.Civil3D.SampleTests` | VSTest | Civil 3D |

MTP samples are `OutputType=Exe` with a scoped `global.json`. VSTest samples are
libraries (adapter discovers `.dll` only) and must **not** sit under an MTP `global.json`.
Each VSTest project compiles the matching host's `HostSmokeTests.cs` via a linked file;
adapter packages are the only project difference.

```text
cd samples/DevTools.NUnit.SampleTests
dotnet test --project DevTools.NUnit.SampleTests.csproj -c Debug.Autodesk.2026 --filter Arithmetic_runs_inside_host

cd samples/DevTools.NUnit.VSTest.SampleTests
dotnet test DevTools.NUnit.VSTest.SampleTests.csproj -c Debug.Autodesk.2026 --filter FullyQualifiedName~Arithmetic_runs_inside_host
```

MTP `--filter` is the NUnit method name. VSTest `--filter` uses `FullyQualifiedName`.
IDE selected-run uses FullName. Runner composes NUnit `TestFilter` XML for the host.

## Behavior

- IDE / `dotnet test --list-tests` discovery reads PE metadata locally. It does
  **not** start a host process. Runner contacts the host only when tests execute.
- `HostLaunch=false` reuses a running host with the same `HostName` +
  `HostVersion`; if none exists, it starts one. `HostLaunch=true` always starts
  a new host (skip reuse).
- MTP exe never runs NUnit test bodies locally; Runner executes them in the host.
- Pipe: `DevTools_{Host}_{Version}_{PID}` (same control pipe family as pytest,
  **not** `DevToolsMcp_*`).
- Methods: `nunit/hello`, `nunit/discover`, `nunit/run`, `nunit/cancel`,
  `nunit/progress`.
- In-host: content-addressed **generation shadow** of the test output plus
  `DevTools.NUnit.Runtime`. Pin **NUnit 4.6.1** (`nunit.framework` file version
  `4.6.1.0`) beside the test assembly. Deploy-folder DLLs stay on
  `AssemblyLoader` (LoadFrom/ALC) — never shadow MahApps/UI.
- Native NUnit 4.6.1 runtime (not `NUnit.Engine`, not a reflective attribute
  subset). Discovery in the IDE is local PE metadata; execution is in-host
  NUnit.

## CLI

CLI tokens and argument layout: `NUnitRunnerCli` in Core. MTP and Runner must not duplicate those flags.

```text
DevTools.NUnit.Runner discover <assembly> --host Revit --version 2024
DevTools.NUnit.Runner run <assembly> --host Revit --version 2026 [--name Arithmetic_runs_inside_host]
```

Runner ships under the ApplicationPlugins bundle `Contents` folder (publish
`DevTools.NUnit.Runner`).

## Relation to ricaun.RevitTest

| Topic | ricaun.RevitTest | DevTools.NUnit |
|-------|------------------|----------------|
| In-host engine | `ricaun.NUnit` reflective (`TestEngine` / attributes) | Native NUnit 4.6.1 runtime; **no** `NUnit.Engine` in host |
| Probe load | **Also shadows**: zip test folder → `%TEMP%\RevitTest\` → extract → `Assembly.LoadFile` on the temp copy (then optional zip-back) | Content-addressed generation shadow of test output + Runtime |
| Transport | Own Console + `PipeTestServer`/`PipeTestClient` (process-named pipe) | Existing `DevToolsPipeServer` + `IHostContextExecutor` |
| IDE surface | VS-oriented + EnvDTE attach | MTP + VSTest samples; no debugger integration |
| Package | ricaun NuGet ecosystem | `RevitDevTool.NUnit` (MTP) NuGet, versioned in the MTP csproj; VSTest adapter in-tree |
| Hosts | Revit-focused product | Revit + AutoCAD family on shared DevTools platform |

**Conflict / coexistence (what actually breaks):**

- ricaun does **not** wait on RevitDevTool. Console waits for **its own** Application
  plugin pipe (`PipeTestClient` ↔ in-Revit `PipeTestServer`). If that pipe never
  appears (plugin not loaded into the chosen Revit process, startup dialogs, hang),
  the run loops until timeout — easy to misread as “waiting for DevTools”.
- Reusing an already-open Revit that started **before** ricaun installed its
  ApplicationPlugins bundle means the ricaun add-in is missing until that process
  is restarted / `NUnit.Open` forces a new Revit.
- Both can load different `nunit.framework` identities in the same Revit (Dynamo,
  DevTools host, ricaun Application). DevTools avoids `NUnit.Engine`
  `FrameworkController` for that reason.
- A project that references both ricaun’s VSTest adapter and `RevitDevTool.NUnit`
  can split ownership; keep host-test projects on one framework.

Do not depend on `ricaun.NUnit` / `ricaun.RevitTest` packages for DevTools NUnit.
The MTP targets fail the build if `ricaun.RevitTest.TestAdapter` is referenced.

The host generation snapshot requires **NUnit 4.6.1** beside the test output
(`nunit.framework` file version `4.6.1.0`). Consumer projects must pin that
package version. `HostTimeout` is the pipe timeout for the **entire**
`nunit/run` request — raise it for large suites (default 60s is only enough
for smoke). `nunit/run` is marshaled through `IHostContextExecutor` with NUnit
`RunOnMainThread`, so test bodies run in the Autodesk API context. WPF
`Dispatcher.Invoke` is not an API context. Host-test projects should read
`Application` from the host's own context type (for example Inspexel
`RevitContext`), not from adapter injection. NUnit MainThread dispatch cannot
cancel an in-flight test.

## Test output

IDE Test Explorer / MTP stdout and the host log pane are different sinks.

| API in the test | IDE stdout (`CaseResult.Output`) | Host pane (tracing on) |
|-----------------|----------------------------------|------------------------|
| `Console.WriteLine` / `TestContext.WriteLine` | NUnit `ITestResult.Output` | Forwarded once at case finish via `Trace.Write` |
| `Trace.WriteLine` / `Debug.WriteLine` | Runtime `NUnitRunTraceScope` merge | Process `Trace.Listeners` |

Do not echo `CaseResult.Output` through Host `ILogger` (duplicates pane Trace;
can include `Revit.exe Error: 0 :`). Pane lines are not prefixed with the NUnit
test name; grouping is the IDE node. Policy: [0017](../decisions/0017-nunit-host-test-output-routing.md).

**DevTools advantages:** shared pipe/DI/execution guard with pytest and MCP;
stamp-keyed per-file shadow (no whole-folder zip round-trip); multi-host Runner
options; one intended adapter package aligned with the rest of RevitDevTool.

## NuGet

| Package | Publish status |
|---------|----------------|
| `RevitDevTool.NUnit` (`DevTools.NUnit.Mtp`) | Independent of installer tags. Version = csproj `<Version>`. Workflow: `PublishNUnit.yml` |
| `DevTools.NUnit.TestAdapter` | **Not published** — in-tree VSTest samples only |
| Core / Host / Runner | Not consumer NuGet APIs |

Bump `<Version>` in `source/DevTools.NUnit.Mtp/DevTools.NUnit.Mtp.csproj`, commit,
then run **Actions → Publish NUnit**. The workflow reads that property, packs
`RevitDevTool.NUnit.{version}.nupkg`, and pushes nuget.org via
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
(OIDC; no long-lived API key). Uncheck **Push to nuget.org** to pack-only.
Do not use installer tags or `scripts/pack.ps1` for this package. Local pack:
`scripts/pack-nunit.ps1`.

One-time nuget.org setup before the first push:

1. nuget.org → account → **Trusted Publishing** → add policy:
   - Repository Owner: `trgiangv`
   - Repository: `RevitDevTool`
   - Workflow File: `PublishNUnit.yml` (file name only)
   - Environment: leave empty
2. Run the workflow with **nuget_user** = nuget.org **profile name** (not email).
   Optional later: store that name as repo secret `NUGET_USER`.
   This repo is public; the policy should show **Active** after Create.

Consumers add NUnit, `RevitDevTool.NUnit`, and `Microsoft.Testing.Platform.MSBuild`;
install [RevitDevTool](https://github.com/trgiangv/RevitDevTool); set the host-run
properties (`HostName`, `HostVersion`, `HostLaunch`, timeouts); and keep a
test-directory `global.json` with `"test": { "runner": "Microsoft.Testing.Platform" }`
so `dotnet test` uses MTP.

## Gaps (not done)

- Microsoft Testing Platform / VSTest IDE matrix (VS / Rider / C# Dev Kit)
- Full NUnit attribute matrix (Theory, explicit, categories, parallel, …)
- Broader automated host-matrix CI (years × hosts) for NUnit beyond sample smoke
- nuget.org listing after the first `Publish NUnit` run (`0.0.1`)
- Package changelog separate from the installer Changelog.md

## Related

- Decision: [`docs/decisions/0016-nunit-native-runtime-and-mtp-first-integration.md`](../decisions/0016-nunit-native-runtime-and-mtp-first-integration.md)
- Output routing: [`docs/decisions/0017-nunit-host-test-output-routing.md`](../decisions/0017-nunit-host-test-output-routing.md)
- Agent notes: [`docs/agents/nunit-host-testing.md`](../agents/nunit-host-testing.md)
- Pytest sibling: [`pytest-bridge.md`](pytest-bridge.md)
