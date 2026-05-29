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
