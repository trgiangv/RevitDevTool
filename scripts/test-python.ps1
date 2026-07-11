<#
.SYNOPSIS
    Run Python parser/server tests.
.DESCRIPTION
    Prefers pixi if available, otherwise falls back to python -m pytest.
    Default target: tests/RevitDevTool.PyServer.Tests (parser/contract tests, not live Revit bridge).
.PARAMETER Path
    Relative path to test directory.
.EXAMPLE
    scripts/test-python.ps1
    scripts/test-python.ps1 -Path tests/some-other-tests
#>
param(
    [string]$Path = "tests/RevitDevTool.PyServer.Tests"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")
$testPath = Join-Path $repoRoot $Path

if (Get-Command pixi -ErrorAction SilentlyContinue) {
    pixi run pytest $testPath
    exit $LASTEXITCODE
}

python -m pytest $testPath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
