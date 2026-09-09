<#
.SYNOPSIS
    Pack the RevitDevTool.TestAdapter NuGet package from DevTools.TestAdapter.
.DESCRIPTION
    Uses <Version> in source/DevTools.TestAdapter/DevTools.TestAdapter.csproj.
    Restores and builds DevTools.NUnit.MTP for all TFMs, then packs the adapter.
    Does not push. Does not run the RevitDevTool installer pack pipeline.
    Pack graph: docs/architecture/Testing/README.md.
.PARAMETER RefreshLocalCache
    Delete the extracted copy of this version from the global packages folder.
    Re-packing the same version otherwise leaves consumers restoring the stale
    extraction from a previous pack.
.EXAMPLE
    scripts/pack-test-adapter.ps1
    scripts/pack-test-adapter.ps1 -OutputDirectory output/nuget
    scripts/pack-test-adapter.ps1 -RefreshLocalCache
#>
param(
    [string]$OutputDirectory = 'output/nuget',
    [switch]$RefreshLocalCache
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot '_lib.ps1')
Assert-RepoRoot

$csproj = Join-RepoPath 'source/DevTools.TestAdapter/DevTools.TestAdapter.csproj'
$outDir = Join-RepoPath $OutputDirectory
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$version = (dotnet msbuild $csproj -nologo -v:q -getProperty:Version).Trim()
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read Version from $csproj"
}

$mtpCsproj = Join-RepoPath 'source/DevTools.NUnit.MTP/DevTools.NUnit.MTP.csproj'
$tunitMtpCsproj = Join-RepoPath 'source/DevTools.TUnit.MTP/DevTools.TUnit.MTP.csproj'

Write-Host "Restoring $csproj, $mtpCsproj, and $tunitMtpCsproj"
dotnet restore $csproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet restore $mtpCsproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet restore $tunitMtpCsproj
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building $mtpCsproj (all TargetFrameworks)"
dotnet build $mtpCsproj -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building $tunitMtpCsproj (all TargetFrameworks)"
dotnet build $tunitMtpCsproj -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Packing RevitDevTool.TestAdapter $version"
dotnet pack $csproj -c Release --no-restore -o $outDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$nupkg = Get-ChildItem -LiteralPath $outDir -Filter "RevitDevTool.TestAdapter.$version.nupkg" |
    Select-Object -First 1
if (-not $nupkg) {
    throw "Expected $outDir/RevitDevTool.TestAdapter.$version.nupkg"
}

Write-Host "Packed $($nupkg.FullName)"

if ($RefreshLocalCache) {
    $cached = Join-Path $env:USERPROFILE ".nuget\packages\revitdevtool.testadapter\$version"
    if (Test-Path -LiteralPath $cached) {
        Remove-Item -LiteralPath $cached -Recurse -Force
        Write-Host "Removed stale extraction $cached"
    }
}

exit 0
