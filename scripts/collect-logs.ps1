<#
.SYNOPSIS
    List or copy RevitDevTool log files from %APPDATA%\RevitDevTool.
.DESCRIPTION
    Without -Destination: lists the 50 most recent .log/.txt files with timestamps and sizes.
    With -Destination: copies those 50 files to the specified directory.
    Log root also includes daemon-tray.log and daemon-stdio.log when the Daemon is used.
.PARAMETER Destination
    Target directory for copying logs. Created if it does not exist.
.EXAMPLE
    scripts/collect-logs.ps1
    scripts/collect-logs.ps1 -Destination ./logs-snapshot
#>
param(
    [string]$Destination = ""
)

$ErrorActionPreference = "Stop"
$logRoot = Join-Path $env:APPDATA "RevitDevTool"

if (-not (Test-Path -LiteralPath $logRoot)) {
    Write-Warning "Log root not found: $logRoot"
    exit 0
}

$recentLogs = Get-ChildItem -LiteralPath $logRoot -Recurse -File -Include *.log, *.txt |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 50

if (-not $Destination) {
    $recentLogs | Select-Object FullName, LastWriteTime, Length
    exit 0
}

$destPath = Resolve-Path -LiteralPath $Destination -ErrorAction SilentlyContinue
if (-not $destPath) {
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $destPath = Resolve-Path -LiteralPath $Destination
}

$recentLogs | Copy-Item -Destination $destPath -Force
