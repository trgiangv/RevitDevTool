<#
.SYNOPSIS
    Pack the RevitDevTool.TestAdapter NuGet package from DevTools.TestAdapter.
.DESCRIPTION
    Uses <Version> in source/DevTools.TestAdapter/DevTools.TestAdapter.csproj.
    Does not push. Does not run the RevitDevTool installer pack pipeline.
.EXAMPLE
    scripts/pack-test-adapter.ps1
    scripts/pack-test-adapter.ps1 -OutputDirectory output/nuget
#>
param(
    [string]$OutputDirectory = 'output/nuget'
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

Write-Host "Packing RevitDevTool.TestAdapter $version"
dotnet pack $csproj -c Release -o $outDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$nupkg = Get-ChildItem -LiteralPath $outDir -Filter "RevitDevTool.TestAdapter.$version.nupkg" |
    Select-Object -First 1
if (-not $nupkg) {
    throw "Expected $outDir/RevitDevTool.TestAdapter.$version.nupkg"
}

Write-Host "Packed $($nupkg.FullName)"
exit 0
