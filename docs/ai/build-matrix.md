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

- Focused host compile: `scripts/agent/build-host.ps1 -Year 2025`.
- Release package: `scripts/agent/pack.ps1`.
- Build pipeline with no args: `dotnet run --project build` compiles all release configurations + publishes DevTools.Daemon.

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

| Command | Daemon behavior |
|---------|-----------------|
| `dotnet run --project build` (no args) | `PublishDaemonModule` publishes + deploys to AppData |
| `dotnet run --project build -- pack` | Same + `CreateBundleModule` packs DevTools.Daemon.exe into bundle zip |
| `dotnet publish source/DevTools.Daemon -c Release` | Direct publish + deploy (csproj target) |

## Kill Host Process Before Deploy

Running Revit or AutoCAD locks loaded DLLs. Any build that deploys to the addin folder will fail or produce stale results if the host is still running.

**Required step before build+deploy:**

```powershell
# Kill Revit (all versions)
Get-Process -Name "Revit" -ErrorAction SilentlyContinue | Stop-Process -Force

# Kill AutoCAD
Get-Process -Name "acad" -ErrorAction SilentlyContinue | Stop-Process -Force
```

This applies to:
- `scripts/agent/build-host.ps1` (MSBuild `DeployRevitAddin` target deploys after build by default)
- `scripts/agent/pack.ps1`
- `dotnet build` without `-p:DeployRevitAddin=false`

NOT needed when building with `-p:DeployRevitAddin=false -p:IsRepackable=false` (compile-only check).

## Compatibility Rule

Any shared `DevTools.*` change can affect all target frameworks. If code is reachable from 2022-2024, verify .NET Framework compatibility and avoid newer BCL APIs unless the repo already has a compatibility helper or package.
