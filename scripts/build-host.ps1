<#
.SYNOPSIS
    Stop the selected host years, then build those hosts.
.DESCRIPTION
    For each year, stops only the Revit or AutoCAD process whose install folder
    matches that product and year. Other running years stay up.
    One host builds that host project. Both hosts build RevitDevTool.slnx once
    for the year, which deploys both add-ins.
.PARAMETER HostApp
    Revit, AutoCAD, or both. Default is both.
.PARAMETER Year
    One or more Autodesk years, or all. Default is all (2022-2027).
.PARAMETER Mode
    Debug or Release. Default is Release.
.EXAMPLE
    scripts/build-host.ps1
    scripts/build-host.ps1 -HostApp Revit -Year 2025 -Mode Debug
    scripts/build-host.ps1 -HostApp Revit,AutoCAD -Year 2025,2026
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string[]]$HostApp = @("Revit", "AutoCAD"),

    [string[]]$Year = @("all"),

    [ValidateSet("Debug", "Release")]
    [string]$Mode = "Release"
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot '_lib.ps1')
Assert-RepoRoot

$allowedHosts = @("Revit", "AutoCAD")
$allowedYears = @("2022", "2023", "2024", "2025", "2026", "2027")
$hosts = @($HostApp | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Select-Object -Unique)
$years = @($Year | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Select-Object -Unique)

foreach ($app in $hosts) {
    if ($allowedHosts -notcontains $app) {
        throw "HostApp must be Revit or AutoCAD. Got '$app'."
    }
}

if ($years.Count -eq 0 -or @($years | Where-Object { $_ -eq "all" }).Count -gt 0) {
    $years = $allowedYears
}
else {
    foreach ($hostYear in $years) {
        if ($allowedYears -notcontains $hostYear) {
            throw "Year must be 2022-2027 or all. Got '$hostYear'."
        }
    }
}

function Stop-HostYear {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [string]$App,
        [string]$HostYear
    )
    $processName = if ($App -eq "Revit") { "Revit" } else { "acad" }
    $productFolder = "$App $HostYear"
    $matching = @()
    $untouched = @()

    foreach ($process in @(Get-Process -Name $processName -ErrorAction SilentlyContinue)) {
        $folder = try { Split-Path -Leaf (Split-Path -Parent $process.Path) } catch { $null }
        if ([string]::Equals($folder, $productFolder, [StringComparison]::OrdinalIgnoreCase)) {
            $matching += $process
        }
        else {
            $untouched += $process
        }
    }

    if ($untouched.Count -gt 0) {
        Write-Output "Leaving other $App versions untouched (PID: $($untouched.Id -join ', '))."
    }

    if ($matching.Count -eq 0) {
        Write-Output "$App $HostYear is not running."
        return
    }

    foreach ($process in $matching) {
        if ($PSCmdlet.ShouldProcess("$($process.Path) (PID $($process.Id))", "Stop $App $HostYear")) {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit(10000) | Out-Null
            Write-Output "Stopped $App $HostYear (PID $($process.Id))."
        }
    }
}

function Build-HostYear {
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$HostYear)
    $configuration = "$Mode.Autodesk.$HostYear"
    if ($hosts.Count -ge 2) {
        $project = Join-RepoPath 'RevitDevTool.slnx'
    }
    elseif ($hosts[0] -eq "Revit") {
        $project = Join-RepoPath 'source/RevitDevTool/RevitDevTool.csproj'
    }
    else {
        $project = Join-RepoPath 'source/AcadDevTool/ACadDevTool.csproj'
    }

    if ($PSCmdlet.ShouldProcess($project, "dotnet build -c $configuration")) {
        dotnet build $project -c $configuration
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}

foreach ($hostYear in $years) {
    foreach ($app in $hosts) {
        Stop-HostYear -App $app -HostYear $hostYear
    }
    Build-HostYear -HostYear $hostYear
}
