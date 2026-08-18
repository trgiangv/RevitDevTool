<#
.SYNOPSIS
    Run .NET test projects.
.DESCRIPTION
    Runs xUnit v3 test projects through their Microsoft Testing Platform
    executable entry points. Without -Project, runs the core focused projects
    sequentially.
    With -Project: runs only the specified test project.
    Stops on first failure.
.PARAMETER Project
    Relative path to a test .csproj from repo root
    (e.g. tests/RevitDevTool.Execution.Tests/RevitDevTool.Execution.Tests.csproj).
.PARAMETER Configuration
    Build configuration for tests (default: Debug).
.EXAMPLE
    scripts/test-dotnet.ps1
    scripts/test-dotnet.ps1 -Project tests/RevitDevTool.Execution.Tests/RevitDevTool.Execution.Tests.csproj
#>
param(
    [string]$Project = "",
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot '_lib.ps1')
Assert-RepoRoot

if ($Project) {
    $target = Join-RepoPath $Project
    if (-not (Test-Path -LiteralPath $target)) {
        throw "Test project not found: $target"
    }
    dotnet run --project $target -c $Configuration -- --no-progress
    exit $LASTEXITCODE
}

$projects = @(
    'tests/DevTools.Execution.Tests/DevTools.Execution.Tests.csproj',
    'tests/DevTools.Testing.Host.Tests/DevTools.Testing.Host.Tests.csproj',
    'tests/DevTools.Telemetry.Tests/DevTools.Telemetry.Tests.csproj'
)

foreach ($relative in $projects) {
    $target = Join-RepoPath $relative
    dotnet run --project $target -c $Configuration -- --no-progress
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
