<#
.SYNOPSIS
    Run one or more .NET test projects.
.DESCRIPTION
    Runs xUnit v3 projects through Microsoft Testing Platform.
    Pass the csproj(s) you need; there is no default suite.
    Extra arguments after the named parameters go to the test host.
    Stops on first failure.
.PARAMETER Project
    Relative path to a test .csproj from repo root. Repeat or comma-separate
    to run several.
.PARAMETER Configuration
    Build configuration (default: Debug).
.EXAMPLE
    scripts/test-dotnet.ps1 -Project tests/DevTools.Execution.Tests/DevTools.Execution.Tests.csproj
    scripts/test-dotnet.ps1 -Project tests/DevTools.AssemblyIsolation.Tests/DevTools.AssemblyIsolation.Tests.csproj -- --filter Directory_source
#>
param(
    [Parameter(Mandatory)]
    [string[]]$Project,
    [string]$Configuration = "Debug",
    [Parameter(ValueFromRemainingArguments)]
    [string[]]$Arguments
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot '_lib.ps1')
Assert-RepoRoot

$mtp = @('--progress', 'off') + @($Arguments)

foreach ($relative in $Project) {
    $target = Join-RepoPath $relative
    if (-not (Test-Path -LiteralPath $target)) {
        throw "Test project not found: $target"
    }

    dotnet run --project $target -c $Configuration -- @mtp
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
