# Build Matrix

## Source Of Truth

- Solution: `RevitDevTool.slnx`.
- Build pipeline: `build/Program.cs` and `build/Modules/*`.
- Required SDK: .NET `10.0.0` from `global.json`.

## Supported Host Configurations

| Autodesk year | Configuration suffix | Target framework |
|---------------|----------------------|------------------|
| 2022 | `Autodesk.2022` | `net48` |
| 2023 | `Autodesk.2023` | `net48` |
| 2024 | `Autodesk.2024` | `net48` |
| 2025 | `Autodesk.2025` | `net8.0-windows` |
| 2026 | `Autodesk.2026` | `net8.0-windows` |
| 2027 | `Autodesk.2027` | `net10.0-windows` |

Valid modes are `Debug` and `Release`, so full names look like `Debug.Autodesk.2025`.

Host `#if` symbols are generated in `props/Revit.targets` and `props/AutoCad.targets`
from `SupportedRevitVersion` / `SupportedAutoCadVersion` (2022–2032 listed;
future years are no-ops until `Directory.Build.props` sets that year).
Building year *Y* defines `REVIT` / `AUTOCAD`, `REVIT{Y}` / `AUTOCAD{Y}`, and
`{HOST}{year}_OR_GREATER` for each listed year from the minimal (default 2022)
through *Y*. Override `RevitVersionMinimal` / `AutoCadVersionMinimal`, or set
`DisableImplicitRevitDefines` / `DisableImplicitAutoCadDefines` to skip.

## Commands

- Focused host compile and deploy: [`scripts/build-host.ps1`](#host-build-script).

- Installer / bundle: `dotnet run --project build -- pack` (`build/Modules/*`, `PublishRelease.yml`).
- TestAdapter NuGet: `dotnet run --project build -- adapter` runs `PackTestAdapterModule` (restore, build the three MTP projects, pack). That module has no dependency on compile, GitVersion, Daemon, TestRunner, or the installer. nuget.org push stays `PublishTestAdapter.yml`. See `docs/architecture/Testing/README.md`.
- Build pipeline with no args: `dotnet run --project build` compiles all release configurations + publishes DevTools.Daemon and DevTools.TestRunner.
- Years stay sequential. Shared projects are solution-mapped to `Debug`/`Release`, so later years reuse `bin/<Debug|Release>/<tfm>`.
- NUnit, TUnit, and MSTest host packaging builds those runtime projects with `Debug` or `Release`, not `*.Autodesk.YYYY`.
- `CompileBaseModule` builds the first release configuration. `CompileExpandModule` builds the later years, still one at a time. Daemon and TestRunner publish after the base module (`BuildProjectReferences=false`, so they do not rebuild shared projects) and overlap the expand module. Bundle creation still waits for every year and both publishes.

## DevTools.Daemon

`DevTools.Daemon` is a standalone HandyControl WPF tray application (`DevTools.Daemon.exe`) that runs outside host processes. It bridges AI clients (Claude Desktop, Cursor) with host applications via named pipes, handles authentication, and manages multi-machine gateway connectivity.

### Publish & Deploy

The csproj has a `DeployDevToolsDaemon` MSBuild target (`AfterTargets="Publish"`) that:
1. Kills any running `DevTools.Daemon.exe` process (file lock prevention)
2. Copies the published single-file executable to `%AppData%\Autodesk\ApplicationPlugins\RevitDevTool.bundle\Contents\`

```bash
# Publish Daemon (triggers deploy automatically)
dotnet publish source/DevTools.Daemon -c Release
```

### Build Characteristics

- Target: `net10.0-windows` / `win-x64`
- Framework-dependent single-file (requires .NET 10 runtime)
- HandyControl WPF tray app with embedded `appsettings.json`
- Properties: `PublishSingleFile=true`, `SelfContained=false`

### Pipeline Integration

| Command | Daemon / Runner behavior |
|---------|-----------------|
| `dotnet run --project build` (no args) | `PublishDaemonModule` + `PublishTestRunnerModule` publish + deploy to AppData Contents |
| `dotnet run --project build -- pack` | Same + `CreateBundleModule` packs both exes into bundle zip |
| `dotnet publish source/DevTools.Daemon -c Release` | Direct Daemon publish + deploy (csproj target) |
| `dotnet publish source/DevTools.TestRunner -c Release` | Direct Runner publish + deploy (csproj target) |

## DevTools.TestRunner

`DevTools.TestRunner` is the host-test CLI (`DevTools.TestRunner.exe`). It locates/reuses Revit or AutoCAD pipes and is installed beside Daemon under bundle `Contents\`.

### Publish & Deploy

The csproj has a `DeployDevToolsTestRunner` MSBuild target (`AfterTargets="Publish"`) that kills a running Runner process (file lock) and copies the single-file exe to `%AppData%\Autodesk\ApplicationPlugins\RevitDevTool.bundle\Contents\`.

```bash
dotnet publish source/DevTools.TestRunner -c Release
```

### Build Characteristics

- Target: `net10.0-windows` / `win-x64`
- Self-contained single-file
- Properties: `PublishSingleFile=true`, `SelfContained=true`

## Host build script

`scripts/build-host.ps1` stops the selected host years, then builds and deploys them. Years run one after another. Other running years stay up. The parameter is `-HostApp` (one dash). With no arguments this is both hosts, every year `2022`–`2027`, configuration `Release`.

| Parameter | Values | Default |
|-----------|--------|---------|
| `-HostApp` | `Revit`, `AutoCAD`, or both (`Revit,AutoCAD`) | both |
| `-Year` | `all`, or one or more of `2022`–`2027` (`2024,2025`) | `all` |
| `-Mode` | `Debug` or `Release` | `Release` |

For each year the script stops only processes whose install folder is `Revit {year}` or `AutoCAD {year}`, then builds:

| `-HostApp` | What is built |
|------------|----------------|
| `Revit` | `source/RevitDevTool/RevitDevTool.csproj` |
| `AutoCAD` | `source/AcadDevTool/ACadDevTool.csproj` |
| both | `RevitDevTool.slnx` once, which deploys both add-ins |

Configuration is `{Mode}.Autodesk.{Year}`.

```powershell
scripts/build-host.ps1
scripts/build-host.ps1 -HostApp Revit -Year 2025 -Mode Debug
scripts/build-host.ps1 -HostApp Revit,AutoCAD -Year 2025,2026
```

A direct `dotnet build` on a **`UseRevit` / `UseAutoCad`** project without compile-only `-p:Deploy*=false` still deploys and will fail if that host year is running. Use `build-host.ps1`, or pass the compile-only props.

NOT needed when:
- Building shared `DevTools.*` (no deploy targets imported), or
- Building a host project with `-p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` (compile-only)

Do not cargo-cult those `-p` flags onto every `dotnet build`. See `.agents/skills/build/SKILL.md`.

## ILRepack

ILRepack lives in `props/ILRepack.targets` (imported for every project). Opt in
with `ILRepackable=true` and list loose DLLs in `RepackBinariesExcludes`. The
driver adds the `ILRepack` PackageReference when that flag is true. Policy for
`/union`, Polyfill, and isolated ALC:
[0019](../decisions/0019-ilrepack-and-polyfill-isolated-alc.md).
`DevTools.TestAdapter` opts in the same way (`ILRepackable` on every TFM,
`RepackBinariesKeep` for `DevTools.Testing.Abstractions.dll`); consumers of
`RevitDevTool.TestAdapter` do not.

Scintilla5.NET 7 natives stay under `runtimes/win-x64/native/` (not output root).
`DevTools.Logging` has a direct `Scintilla5.NET` PackageReference so
`build/scintilla5.net.targets` copies them (needed on net48). `Common.props`
then drops `win-x86` / `win-arm64` on every TFM after `CopyFilesToOutputDirectory`.

## Compatibility Rule

Any shared `DevTools.*` change can affect all target frameworks. Polyfill covers
missing BCL APIs on every TFM (`ThrowIfNull` via `PolyArgumentExceptions`). After
edits, compile via `.agents/skills/build/SKILL.md` (multi-TFM `Debug`, or Autodesk
2022/2025/2027 for host API projects). Avoid newer BCL APIs only when compile
fails and no Polyfill/helper exists.

## Agent compile verify

Agents run `dotnet build` on touched projects per the **build** skill: shared
libraries without deploy props; host `UseRevit`/`UseAutoCad` projects with
deploy/ILRepack off for compile-only proof. Spot-check `2022` / `2025` / `2027`
for host API projects when TFM-sensitive. Full command reference: `verification.md`.
