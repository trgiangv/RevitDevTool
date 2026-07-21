<#
.SYNOPSIS
    Run Python parser/server tests in this repo.
.DESCRIPTION
    Uses pixi from the repo-root pixi.toml. Default target:
    tests/RevitDevTool.PyServer.Tests (parser/contract tests, not live Revit bridge).
    For client pytest against a live host, use RevitDevTool.PyTest with uv instead.
.PARAMETER Path
    Relative path to test directory from repo root.
.EXAMPLE
    scripts/test-python.ps1
    scripts/test-python.ps1 -Path tests/RevitDevTool.PyServer.Tests
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
    throw "pixi is required for scripts/test-python.ps1. Install pixi or run from a pixi-enabled shell."
}

Push-Location $RepoRoot
try {
    pixi run pytest $testPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
