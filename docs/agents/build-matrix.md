# Build Matrix

## Source Of Truth

- Solution: `RevitDevTool.slnx`.
- Build pipeline: `build/Program.cs` and `build/Modules/*`.
- Required SDK: `global.json`.

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
- `build/` takes `<target> <product>`; run `dotnet run --project build` for the list. Products: `daemon`, `addin`, `nettest`, `pytest`. Releases: [release.md](release.md).
- Add-in bundle: `dotnet run --project build -- compile addin`, then `pack addin`, into `output/addin`.
- Daemon: `dotnet run --project build -- pack daemon` into `output/daemon` (Velopack).
- TestAdapter NuGet: `dotnet run --project build -- pack nettest` runs `PackTestAdapterModule` (restore, pack; the csproj builds the MTP projects and framework runtimes and publishes the TestRunner into `tools/runner`). See `docs/architecture/Testing/README.md`.
- `ci <product>` compiles and runs the product's tests, as `ci.yml` does. The TestRunner ships only in the TestAdapter package.
- The add-in compile builds the solution with `-t:RevitDevTool;ACadDevTool`, so only the host projects and their references build, with the solution's configuration mapping.
- On one machine host versions stay sequential. Shared projects are solution-mapped to `Debug`/`Release`, so later host versions reuse `bin/<Debug|Release>/<tfm>`.
- Targets are stages: `compile`, `test`, `ci` (compile + test), `pack`, `release` (all three). The add-in compile always publishes into `bin/Release.Autodesk.<v>/publish` with the run version; `pack addin` only bundles what is there, so run `compile addin` first.
- `ci.yml` runs each add-in host version on its own runner instead:

  | Job | Command |
  |-----|---------|
  | `addin <v>` | `compile addin --host-version <v>`; on push the publish folders upload as `addin-<v>` |
  | `addin tests` | `test addin` |
  | `addin bundle` (push) | downloads every `addin-*`, then `pack addin` |

  The host versions come from the `Release.Autodesk.*` build types in `RevitDevTool.slnx`. `release.yml` still runs `release addin` on one runner.
- NUnit, TUnit, and MSTest host packaging builds those runtime projects with `Debug` or `Release`, not `*.Autodesk.YYYY`.
- `CompileBaseModule` builds the first release configuration. `CompileExpandModule` builds the later years, still one at a time. Bundle creation waits for every year.
- The add-in compile of `build/` sets `DeployAddin=false` and `PublishAddin=true`. `Revit.targets` and `AutoCad.targets` both read those two properties; each project imports only one of those files. The bundle zip is packed from `bin/**/publish`, so `build/` does not copy into `%AppData%\Autodesk\ApplicationPlugins\RevitDevTool.bundle`. A solution build of both hosts into that one folder races `RemoveDir` on `Contents\<year>` (`MSB3231`). Local host deploy stays `scripts/build-host.ps1`.

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

| Command | Daemon behavior |
|---------|-----------------|
| `dotnet run --project build -- ci daemon` | `CompileProductModule` builds the Daemon, then its tests run |
| `dotnet run --project build -- pack daemon` | `PackDaemonModule` publishes the Daemon self-contained and packs it with Velopack into `output/daemon` |
| `dotnet publish source/DevTools.Daemon -c Release` | Direct Daemon publish + deploy (csproj target) |

## DevTools.TestRunner

`DevTools.TestRunner` is the host-test CLI (`DevTools.TestRunner.exe`). It locates/reuses Revit or AutoCAD pipes. It ships only inside `RevitDevTool.TestAdapter` at `tools/runner/` (the csproj `PackRunner` target publishes it during `dotnet pack`); no command deploys it to the bundle or the Velopack folder.

### Build Characteristics

- Target: `net10.0-windows` / `win-x64` (major in `props/TestingRuntime.props`)
- Framework-dependent single-file, `RollForward=Major`
- Properties: `PublishSingleFile=true`, `SelfContained=false`

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

A direct `dotnet build` on a `UseRevit` / `UseAutoCad` project still deploys, and fails if that host year is running. Use `build-host.ps1`, or pass the compile-only props from `.agents/skills/build/SKILL.md` (shared `DevTools.*` need neither). `Clean` removes the whole AppData `RevitDevTool.bundle`.

## ILRepack

ILRepack lives in `props/ILRepack.targets` (imported for every project). Opt in
with `ILRepackable=true` and list loose DLLs in `RepackBinariesExcludes`. The
driver adds the `ILRepack` PackageReference when that flag is true. Policy for
`/union`, Polyfill, and isolated ALC:
[0019](../decisions/0019-ilrepack-and-polyfill-isolated-alc.md).
`DevTools.TestAdapter` opts in the same way (`ILRepackable` on every TFM,
`RepackBinariesKeep` for `DevTools.Testing.Abstractions.dll`); consumers of
`RevitDevTool.TestAdapter` do not.

Scintilla natives stay under `runtimes/win-x64/native/` and
`runtimes/win-arm64/native/` (not output root). `ZLogger.Scintilla` deploys them on
every TFM, net48 included, and leaves out `win-x86`; set `ScintillaNativeRuntimes`
to change the list. No direct `Scintilla5.NET` reference is needed.

### Merge log

`/union` makes `ILRepack.exe` print `WARNING:` lines on stdout for duplicate
resources and for members it did not carry over. They are tool chatter, not build
warnings (MSBuild reports `0 Warning(s)`); `RepackAddinFiles` logs them at low
importance, so the default build shows only `Repacking <dll>`. Re-read them with
`dotnet build -v detailed`.

## Compatibility

Any shared `DevTools.*` change can affect every target framework. Polyfill covers
missing BCL APIs on every TFM (`ThrowIfNull` via `PolyArgumentExceptions`). Compile
with `.agents/skills/build/SKILL.md` (multi-TFM `Debug`, or Autodesk 2022, 2025, and
2027 for host API projects). Avoid a newer BCL API only when the compile fails and
no Polyfill or helper exists.
