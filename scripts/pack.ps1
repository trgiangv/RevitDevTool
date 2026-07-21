<#
.SYNOPSIS
    Run the full release pack pipeline via ModularPipelines.
.DESCRIPTION
    Executes the build orchestrator with the 'pack' argument:
    Clean -> CreateBundle (all Release.Autodesk.* configs + DevTools.Daemon) -> CreateInstaller.
    Output goes to build/output/. Run scripts/kill-host.ps1 first if hosts are running.
.EXAMPLE
    scripts/kill-host.ps1
    scripts/pack.ps1
#>
param()

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot '_lib.ps1')
Assert-RepoRoot

$buildRoot = Join-RepoPath 'build'

Push-Location $buildRoot
try {
    dotnet run -c Release pack
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
