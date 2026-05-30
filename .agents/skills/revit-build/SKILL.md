---
name: revit-build
description: Build and test Revit add-ins with correct configurations for Revit 2022-2027 (NET 4.8, NET 8.0, NET 10.0). Use when building, compiling, testing, packaging Revit projects, or when the user mentions Revit versions, build configurations, testing across multiple Revit versions, or the ModularPipelines build system.
---

# Revit Build Configuration

## Supported Versions

| Revit Version | Target Framework | Configuration Pattern |
|---------------|------------------|-----------------------|
| 2022-2024 | `net48` | `*.Autodesk.20XX` |
| 2025-2026 | `net8.0-windows` | `*.Autodesk.20XX` |
| 2027 | `net10.0-windows` | `*.Autodesk.2027` |

Configurations follow `{Debug|Release}.Autodesk.{year}` (e.g. `Release.Autodesk.2025`).

Solution file: `RevitDevTool.slnx` (XML-based slnx format, not legacy `.sln`).

## MSBuild Structure

### Directory.Build.props (repo root)

- Central package management (`ManagePackageVersionsCentrally`)
- `LangVersion: latest`, `Nullable: enable`, `ImplicitUsings: true`, `TreatWarningsAsErrors: true`
- Maps configuration year → `RevitVersion`, `TargetFramework`, plus `AutoCadVersion`, `Civil3dVersion`, `NavisworksVersion`
- `AppendTargetFrameworkToOutputPath: false` for Autodesk configurations

### Directory.Build.targets (repo root)

Conditionally imports product-specific props/targets based on project flags:

```xml
<Import Condition="'$(UseRevit)' == 'true'" Project="props\Revit.props" />
<Import Condition="'$(UseRevit)' == 'true'" Project="props\Revit.targets" />
```

Projects opt in by setting `<UseRevit>true</UseRevit>` in their `.csproj`.

### props/Revit.props

Imports `Common.props` then adds Revit API packages:

- `Revit_All_Main_Versions_API_x64` — main Revit API (replaces the old `Nice3point.Revit.Api.*` packages), with `ExcludeAssets="runtime"`
- `Nice3point.Revit.Extensions` — extension helpers
- `Nice3point.Revit.Api.UIFrameworkServices` — UI framework interop

All use `VersionOverride="$(RevitVersion).*"` for version-per-configuration resolution.

Also defines global usings: `Autodesk.Revit.DB`, `Autodesk.Revit.UI`, `JetBrains.Annotations`, `System`.

### props/Revit.targets

Provides MSBuild targets for Revit add-in projects:

1. **Debug/Release detection** — sets `DebugSymbols`/`Optimize` and `DefineConstants` for custom configurations
2. **ILRepack** — merges assemblies post-build when `IsRepackable=true` (disabled for Revit 2027 due to isolated context)
3. **Publish/Deploy** — copies addin files to `$(PublishDir)` and optionally deploys to `%AppData%\Autodesk\Revit\Addins\$(RevitVersion)\`
4. **Define constants** — auto-generates `REVIT20XX` and `REVIT20XX_OR_GREATER` preprocessor symbols

## Polyfill Package

Uses **`Polyfill`** (`GlobalPackageReference` in `Directory.Packages.props`) for modern C# features on older frameworks. **PolySharp has been removed** — do not reference or suggest PolySharp.

## Build System (ModularPipelines)

Build project: `build/Build.csproj` targeting `net10.0`.

### CLI Commands

| Command | Modules |
|---------|---------|
| *(no args)* | `CompileProjectModule` |
| `test` | `TestProjectModule` |
| `pack` | `CleanProjectModule` → `CreateBundleModule` + `PublishMcpServerModule` → `CreateInstallerModule` |
| `publish` | `PublishGithubModule` |

### Key Modules

- **ResolveConfigurationsModule**: Reads `Release.Autodesk.*` configs from the `.slnx` solution file
- **ResolveVersioningModule**: Resolves version from `BuildOptions.Version` or GitVersion tool
- **CompileProjectModule**: Builds solution for each resolved configuration with version properties
- **TestProjectModule**: Runs tests per configuration (skipped in CI)
- **CleanProjectModule**: Cleans `bin`/`obj` and output directory (skipped in CI)
- **CreateBundleModule**: Creates Autodesk `.bundle` package from publish outputs
- **PublishMcpServerModule**: Publishes MCP server as self-contained executable
- **CreateInstallerModule**: Creates `.msi` installer via WiX toolset
- **PublishGithubModule**: Creates GitHub release with changelog and uploads artifacts

## Build Commands

### CRITICAL: Kill host process before build+deploy

The built DLL is loaded into the Revit/AutoCAD process. A running host **locks** the DLL file and prevents overwrite. Before building a configuration that deploys to the addin folder, you MUST kill the corresponding process:

```powershell
# Find and kill Revit for a specific version before building that version
Get-Process -Name "Revit" -ErrorAction SilentlyContinue | Stop-Process -Force

# If you know the specific PID (e.g. from Named Pipe name Revit_2025_16544):
Stop-Process -Id 16544 -Force -ErrorAction SilentlyContinue

# For AutoCAD:
Get-Process -Name "acad" -ErrorAction SilentlyContinue | Stop-Process -Force
```

**When to kill:**
- Before `scripts/agent/build-host.ps1` (always deploys to addin folder)
- Before `dotnet build` with `DeployRevitAddin=true` (default for Debug builds)
- Before `dotnet run --project build/Build.csproj -- pack`
- NOT needed for builds with `-p:DeployRevitAddin=false`

**Flow:** Kill process → Build → (optionally) Restart host to test

### Commands

```bash
# Compile all Release configurations
dotnet run --project build/Build.csproj

# Run tests
dotnet run --project build/Build.csproj -- test

# Pack (clean + bundle + MCP server + installer)
dotnet run --project build/Build.csproj -- pack

# Publish to GitHub
dotnet run --project build/Build.csproj -- publish

# Build specific configuration manually
dotnet build RevitDevTool.slnx -c "Release.Autodesk.2025"
```

IDE run configs: `.run/Compile.run.xml` (no args) and `.run/Pack.run.xml` (`pack` arg) for JetBrains Rider.

## Testing Workflow

For **fast test builds** (compile-only, skip ILRepack and deploy), disable `IsRepackable` and `DeployRevitAddin`:

```bash
dotnet build RevitDevTool.slnx -c "Release.Autodesk.2024" -p:IsRepackable=false -p:DeployRevitAddin=false
dotnet build RevitDevTool.slnx -c "Release.Autodesk.2025" -p:IsRepackable=false -p:DeployRevitAddin=false
```

For **release builds** that deploy into Revit's addin folder, use defaults (no property overrides):

```bash
dotnet build RevitDevTool.slnx -c "Release.Autodesk.2024"
dotnet build RevitDevTool.slnx -c "Release.Autodesk.2025"
```

Build at minimum two configurations spanning both framework eras (net48 + net8.0-windows).

Or run the full compile pipeline: `dotnet run --project build/Build.csproj`

## Troubleshooting

- **Build fails**: Verify NuGet packages available. Revit API packages use `VersionOverride="$(RevitVersion).*"`.
- **Framework mismatch**: Configuration must match pattern `Release.Autodesk.2025`, not old `Release R25`.
- **ILRepack fails on Revit 2027**: Expected — `IsRepackable` is disabled for 2027 due to isolated context causing `System.BadImageFormatException`.
- **Polyfill issues on net48**: Ensure `Polyfill` GlobalPackageReference is in `Directory.Packages.props`. Do not use PolySharp.
- **Missing Revit API**: The project uses `Revit_All_Main_Versions_API_x64` (not the old `Nice3point.Revit.Api.*` API packages).
