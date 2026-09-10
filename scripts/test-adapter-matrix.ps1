<#
.SYNOPSIS
    Build + host-free discovery proof for the TestAdapter consumer surface.

.DESCRIPTION
    Packs RevitDevTool.TestAdapter into the local NuGet feed (same surface an
    end user restores), then runs every sample test project through one Autodesk
    configuration per runtime the package ships: 2024 -> net48, 2025 -> net8,
    2027 -> net10. Each project is restored from that nupkg, built, and the
    produced testhost is asked for --list-tests, which must succeed without a
    running host.

.EXAMPLE
    scripts/test-adapter-matrix.ps1
    scripts/test-adapter-matrix.ps1 -Years 2025 -Flavor Release
#>
param(
    [string[]]$Years = @('2024', '2025', '2027'),
    [ValidateSet('Debug', 'Release')]
    [string]$Flavor = 'Debug',
    [string[]]$Projects = @(
        'samples/DevTools.NUnit.SampleTests/DevTools.NUnit.SampleTests.csproj',
        'samples/DevTools.TUnit.SampleTests/DevTools.TUnit.SampleTests.csproj',
        'samples/DevTools.NUnit.Civil3D.SampleTests/DevTools.NUnit.Civil3D.SampleTests.csproj',
        'samples/DevTools.TUnit.Civil3D.SampleTests/DevTools.TUnit.Civil3D.SampleTests.csproj'
    )
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Write-Host 'Packing RevitDevTool.TestAdapter into output/nuget (end-user PackageReference surface)...'
& (Join-Path $PSScriptRoot 'pack-test-adapter.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$rows = @()
$failures = 0

foreach ($year in $Years) {
    $configuration = "$Flavor.Autodesk.$year"

    foreach ($relative in $Projects) {
        $project = Join-Path $root ($relative -replace '/', '\')
        $name = [IO.Path]::GetFileNameWithoutExtension($project)
        $status = 'ok'
        $tests = ''

        $build = & dotnet restore $project -p:Configuration=$configuration --force 2>&1
        if ($LASTEXITCODE -ne 0) {
            $status = 'restore failed'
            $build | Select-Object -Last 15 | ForEach-Object { Write-Host $_ }
        }
        else {
            $build = & dotnet build $project -c $configuration --no-restore -v q 2>&1
            if ($LASTEXITCODE -ne 0) {
                $status = 'build failed'
                $build | Select-Object -Last 15 | ForEach-Object { Write-Host $_ }
            }
            else {
                $binDir = Join-Path (Split-Path -Parent $project) "bin\$configuration"
                $exe = Get-ChildItem -LiteralPath $binDir -Recurse -Filter "$name.exe" -ErrorAction SilentlyContinue |
                    Select-Object -First 1

                if (-not $exe) {
                    $status = 'no testhost'
                }
                else {
                    $listed = & $exe.FullName --list-tests 2>&1
                    if ($LASTEXITCODE -ne 0) {
                        $status = 'discovery failed'
                        $listed | Select-Object -Last 20 | ForEach-Object { Write-Host $_ }
                    }
                    else {
                        $match = [regex]::Match(($listed -join "`n"), '(\d+)\s+test')
                        $tests = if ($match.Success) { $match.Groups[1].Value } else { '?' }
                    }
                }
            }
        }

        if ($status -ne 'ok') { $failures++ }
        $rows += [pscustomobject]@{
            Configuration = $configuration
            Project       = $name
            Status        = $status
            Tests         = $tests
        }
    }
}

$rows | Format-Table -AutoSize

if ($failures -gt 0) {
    Write-Host "$failures matrix entries failed." -ForegroundColor Red
    exit 1
}

Write-Host 'TestAdapter matrix passed (build + host-free discovery).' -ForegroundColor Green
exit 0
