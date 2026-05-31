<#
.SYNOPSIS
    Build RevitDevTool.slnx for a single Autodesk host year.
.DESCRIPTION
    Compiles the solution with configuration {Mode}.Autodesk.{Year}.
    MSBuild targets may deploy to the addin folder — kill host processes first
    if Revit/AutoCAD is running (DLL file locks).
.PARAMETER Year
    Autodesk product year (2022-2024 = net48, 2025-2026 = net8.0, 2027 = net10.0).
.PARAMETER Mode
    Debug or Release build mode.
.EXAMPLE
    scripts/agent/build-host.ps1 -Year 2025
    scripts/agent/build-host.ps1 -Year 2024 -Mode Release
#>
param(
    [ValidateSet("2022", "2023", "2024", "2025", "2026", "2027")]
    [string]$Year = "2025",

    [ValidateSet("Debug", "Release")]
    [string]$Mode = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$solution = Join-Path $repoRoot "RevitDevTool.slnx"
$configuration = "$Mode.Autodesk.$Year"

dotnet build $solution -c $configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
