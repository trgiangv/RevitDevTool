<#
.SYNOPSIS
    Run PyServer parser and contract tests in this repo.
.DESCRIPTION
    Uses pixi from the repo-root pixi.toml. Default target:
    tests/RevitDevTool.PyServer.Tests. This is not the live-host pytest
    plugin in the RevitDevTool.PyTest repo.
.PARAMETER Path
    Relative path to the test directory from the repo root.
.EXAMPLE
    scripts/test-pyserver.ps1
    scripts/test-pyserver.ps1 -Path tests/RevitDevTool.PyServer.Tests
#>
param(
    [string]$Path = "tests/RevitDevTool.PyServer.Tests"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot '_lib.ps1')
Assert-RepoRoot

$testPath = Join-RepoPath $Path
if (-not (Test-Path -LiteralPath $testPath)) {
    throw "Test path not found: $testPath"
}

if (-not (Get-Command pixi -ErrorAction SilentlyContinue)) {
    throw "pixi is required for scripts/test-pyserver.ps1. Install pixi or run from a pixi-enabled shell."
}

Push-Location $RepoRoot
try {
    pixi run pytest $testPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
