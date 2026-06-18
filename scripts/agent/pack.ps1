<#
.SYNOPSIS
    Run the full release pack pipeline via ModularPipelines.
.DESCRIPTION
    Executes the build orchestrator with the 'pack' argument:
    Clean → CreateBundle (all Release.Autodesk.* configs + DevTools.Daemon) → CreateInstaller.
    Output goes to build/output/.
.EXAMPLE
    scripts/agent/pack.ps1
#>
param()

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$buildRoot = Join-Path $repoRoot "build"

Push-Location $buildRoot
try {
    dotnet run -c Release pack
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
