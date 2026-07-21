<#
.SYNOPSIS
    Record startup profiling notes and collect related logs.
.DESCRIPTION
    Placeholder for manual startup profiling workflow. Prints host/year/timestamp context
    then delegates to collect-logs.ps1 to list recent log files.
    This script does not perform automated timing - it provides a stable anchor
    for recording and reviewing startup behavior.
.PARAMETER HostApp
    Target host application (Revit or AutoCAD).
.PARAMETER Year
    Autodesk product year.
.EXAMPLE
    scripts/startup-profile.ps1 -HostApp Revit -Year 2025
#>
param(
    [ValidateSet("Revit", "AutoCAD")]
    [string]$HostApp = "Revit",

    [ValidateSet("2022", "2023", "2024", "2025", "2026", "2027")]
    [string]$Year = "2025"
)

$ErrorActionPreference = "Stop"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

Write-Output "Startup profile note"
Write-Output "Timestamp: $timestamp"
Write-Output "Host: $HostApp $Year"
Write-Output "Use this script as the stable place to record startup profiling steps."
Write-Output "Related logs:"
& (Join-Path $PSScriptRoot "collect-logs.ps1")
