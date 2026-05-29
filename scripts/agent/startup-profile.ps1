param(
    [ValidateSet("Revit", "AutoCAD")]
    [string]$Host = "Revit",

    [ValidateSet("2022", "2023", "2024", "2025", "2026", "2027")]
    [string]$Year = "2025"
)

$ErrorActionPreference = "Stop"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

Write-Output "Startup profile note"
Write-Output "Timestamp: $timestamp"
Write-Output "Host: $Host $Year"
Write-Output "Use this script as the stable place to record startup profiling steps."
Write-Output "Related logs:"
& (Join-Path $PSScriptRoot "collect-logs.ps1")
