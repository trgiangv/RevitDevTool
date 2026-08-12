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

## Commands

- Focused host compile: `scripts/build-host.ps1 -Year 2025`.
- Release package: `scripts/pack.ps1`.
- Build pipeline with no args: `dotnet run --project build` compiles all release configurations + publishes DevTools.Daemon and DevTools.NUnit.Runner.

## DevTools.Daemon

`DevTools.Daemon` is a standalone WPF tray application (`DevTools.Daemon.exe`) that runs outside host processes. It bridges AI clients (Claude Desktop, Cursor) with host applications via named pipes, handles authentication, and manages multi-machine gateway connectivity.

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
- Self-contained single-file (~52MB)
- WPF tray app with embedded `appsettings.json` (CI/CD injects secrets)
- Properties: `PublishSingleFile=true`, `SelfContained=true`

### Pipeline Integration

| Command | Daemon / Runner behavior |
|---------|-----------------|
| `dotnet run --project build` (no args) | `PublishDaemonModule` + `PublishNUnitRunnerModule` publish + deploy to AppData Contents |
| `dotnet run --project build -- pack` | Same + `CreateBundleModule` packs both exes into bundle zip |
| `dotnet publish source/DevTools.Daemon -c Release` | Direct Daemon publish + deploy (csproj target) |
| `dotnet publish source/DevTools.NUnit.Runner -c Release` | Direct Runner publish + deploy (csproj target) |

## DevTools.NUnit.Runner

`DevTools.NUnit.Runner` is the host NUnit CLI (`DevTools.NUnit.Runner.exe`). It locates/reuses Revit or AutoCAD pipes and is installed beside Daemon under bundle `Contents\`.

### Publish & Deploy

The csproj has a `DeployDevToolsNUnitRunner` MSBuild target (`AfterTargets="Publish"`) that kills a running Runner process (file lock) and copies the single-file exe to `%AppData%\Autodesk\ApplicationPlugins\RevitDevTool.bundle\Contents\`.

```bash
dotnet publish source/DevTools.NUnit.Runner -c Release
```

### Build Characteristics

- Target: `net10.0-windows` / `win-x64`
- Self-contained single-file
- Properties: `PublishSingleFile=true`, `SelfContained=true`

## Kill Host Process Before Deploy

Running Revit or AutoCAD locks loaded DLLs. Any build that deploys to the addin folder will fail or produce stale results if the host is still running.

**Required step before build+deploy** — use `scripts/kill-host.ps1`:

```powershell
scripts/kill-host.ps1                # Kill both Revit and AutoCAD
scripts/kill-host.ps1 -HostApp Revit # Kill only Revit
```

This applies to:
- `scripts/build-host.ps1` (MSBuild `DeployRevitAddin`/`DeployAutoCadBundle` target deploys after build)
- `scripts/pack.ps1`
- `dotnet build` on a **`UseRevit` / `UseAutoCad`** project without compile-only `-p:Deploy*=false`

NOT needed when:
- Building shared `DevTools.*` (no deploy targets imported), or
- Building a host project with `-p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:IsRepackable=false` (compile-only)

Do not cargo-cult those `-p` flags onto every `dotnet build`. See `.agents/skills/build/SKILL.md`.

## ILRepack

ILRepack merges dependencies into a single DLL for host add-ins. **Disabled for 2027** due to Revit/AutoCAD isolated context causing `System.BadImageFormatException` with repacked assemblies. Property: `IsRepackable` (auto-set false when `RevitVersion`/`AutoCadVersion` >= 2027).

## Compatibility Rule

Any shared `DevTools.*` change can affect all target frameworks. Polyfill covers most modern C# on net48. After edits, compile via `.agents/skills/build/SKILL.md` (multi-TFM `Debug`, or Autodesk 2022/2025/2027 for host API projects). Avoid newer BCL APIs only when compile fails and no Polyfill/helper exists.

## Agent compile verify

Agents run `dotnet build` on touched projects per the **build** skill: shared
libraries without deploy props; host `UseRevit`/`UseAutoCad` projects with
deploy/ILRepack off for compile-only proof. Spot-check `2022` / `2025` / `2027`
for host API projects when TFM-sensitive. Full command reference: `verification.md`.
