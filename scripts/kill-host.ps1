<#
.SYNOPSIS
    Stop one Autodesk host version before a deploy build.
.DESCRIPTION
    Stops only processes whose executable folder matches the requested product
    and year. Other running Revit/AutoCAD versions are always left untouched.
.PARAMETER HostApp
    Host product to stop.
.PARAMETER Year
    Exact Autodesk product year to stop.
.EXAMPLE
    scripts/kill-host.ps1 -HostApp Revit -Year 2025
    scripts/kill-host.ps1 -HostApp AutoCAD -Year 2025
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [ValidateSet("Revit", "AutoCAD")]
    [string]$HostApp,

    [Parameter(Mandatory)]
    [ValidateSet("2022", "2023", "2024", "2025", "2026", "2027")]
    [string]$Year
)

$ErrorActionPreference = "Stop"

$processName = if ($HostApp -eq "Revit") { "Revit" } else { "acad" }
$productFolder = "$HostApp $Year"
$matching = @()
$untouched = @()

foreach ($process in @(Get-Process -Name $processName -ErrorAction SilentlyContinue)) {
    $folder = try { Split-Path -Leaf (Split-Path -Parent $process.Path) } catch { $null }
    if ([string]::Equals($folder, $productFolder, [StringComparison]::OrdinalIgnoreCase)) {
        $matching += $process
    } else {
        $untouched += $process
    }
}

if ($untouched.Count -gt 0) {
    Write-Output "Leaving other $HostApp versions untouched (PID: $($untouched.Id -join ', '))."
}

if ($matching.Count -eq 0) {
    Write-Output "$HostApp $Year is not running."
    exit 0
}

foreach ($process in $matching) {
    if ($PSCmdlet.ShouldProcess("$($process.Path) (PID $($process.Id))", "Stop $HostApp $Year")) {
        Stop-Process -Id $process.Id -Force
        $process.WaitForExit(10000) | Out-Null
        Write-Output "Stopped $HostApp $Year (PID $($process.Id))."
    }
}
