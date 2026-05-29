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
