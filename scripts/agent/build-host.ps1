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
