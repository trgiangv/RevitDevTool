<#
.SYNOPSIS
    Kill running host processes before deploy builds.
.DESCRIPTION
    Stops Revit and/or AutoCAD processes that lock DLLs in the addin folder.
    Run this before any build that deploys to the bundle/addin directory.
.PARAMETER HostApp
    Which host to kill. Default: All (both Revit and AutoCAD).
.EXAMPLE
    scripts/agent/kill-host.ps1
    scripts/agent/kill-host.ps1 -HostApp Revit
    scripts/agent/kill-host.ps1 -HostApp AutoCAD
#>
param(
    [ValidateSet("All", "Revit", "AutoCAD")]
    [string]$HostApp = "All"
)

$ErrorActionPreference = "Stop"

$targets = @()
if ($HostApp -eq "All" -or $HostApp -eq "Revit") {
    $targets += "Revit"
}
if ($HostApp -eq "All" -or $HostApp -eq "AutoCAD") {
    $targets += "acad"
}

foreach ($name in $targets) {
    $procs = Get-Process -Name $name -ErrorAction SilentlyContinue
    if ($procs) {
        Write-Output "Stopping $name (PID: $($procs.Id -join ', '))..."
        $procs | Stop-Process -Force
        Start-Sleep -Seconds 2
        Write-Output "$name stopped."
    } else {
        Write-Output "$name not running."
    }
}

Write-Output "Done. Safe to build with deploy."
