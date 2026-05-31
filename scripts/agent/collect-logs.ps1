<#
.SYNOPSIS
    List or copy RevitDevTool log files from %APPDATA%\RevitDevTool.
.DESCRIPTION
    Without -Destination: lists the 50 most recent .log/.txt files with timestamps and sizes.
    With -Destination: copies those 50 files to the specified directory.
.PARAMETER Destination
    Target directory for copying logs. Created if it does not exist.
.EXAMPLE
    scripts/agent/collect-logs.ps1
    scripts/agent/collect-logs.ps1 -Destination ./logs-snapshot
#>
param(
    [string]$Destination = ""
)

$ErrorActionPreference = "Stop"
$logRoot = Join-Path $env:APPDATA "RevitDevTool"

if (-not (Test-Path $logRoot)) {
    Write-Warning "Log root not found: $logRoot"
    exit 0
}

if (-not $Destination) {
    Get-ChildItem $logRoot -Recurse -File -Include *.log,*.txt |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 50 FullName, LastWriteTime, Length
    exit 0
}

$destPath = Resolve-Path -LiteralPath $Destination -ErrorAction SilentlyContinue
if (-not $destPath) {
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    $destPath = Resolve-Path -LiteralPath $Destination
}

Get-ChildItem $logRoot -Recurse -File -Include *.log,*.txt |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 50 |
    Copy-Item -Destination $destPath -Force
