<#
.SYNOPSIS
    Pack the RevitDevTool.NUnit NuGet package from DevTools.NUnit.Mtp.
.DESCRIPTION
    Uses <Version> in source/DevTools.NUnit.Mtp/DevTools.NUnit.Mtp.csproj.
    Does not push. Does not run the RevitDevTool installer pack pipeline.
.EXAMPLE
    scripts/pack-nunit.ps1
    scripts/pack-nunit.ps1 -OutputDirectory output/nuget
#>
param(
    [string]$OutputDirectory = 'output/nuget'
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot '_lib.ps1')
Assert-RepoRoot

$csproj = Join-RepoPath 'source/DevTools.NUnit.Mtp/DevTools.NUnit.Mtp.csproj'
$outDir = Join-RepoPath $OutputDirectory
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$version = (dotnet msbuild $csproj -nologo -v:q -getProperty:Version).Trim()
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read Version from $csproj"
}

Write-Host "Packing RevitDevTool.NUnit $version"
dotnet pack $csproj -c Release -o $outDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$nupkg = Get-ChildItem -LiteralPath $outDir -Filter "RevitDevTool.NUnit.$version.nupkg" |
    Select-Object -First 1
if (-not $nupkg) {
    throw "Expected $outDir/RevitDevTool.NUnit.$version.nupkg"
}

Write-Host "Packed $($nupkg.FullName)"
exit 0
