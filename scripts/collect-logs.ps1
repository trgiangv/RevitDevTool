<#
.SYNOPSIS
    List or copy RevitDevTool and related diagnostic logs.
.DESCRIPTION
    Collects the newest files from each root, up to -Count per root:
    - %APPDATA%\RevitDevTool (host log_* / crash_*, including .json, and Daemon logs\)
    - a custom fileLogging.logFolder from each {year}\Settings\LogConfig.json
    - %TEMP%\RevitDevTool-*.txt (code_mode full output)
    - %LOCALAPPDATA%\Autodesk\Revit\*\Journals\journal*.txt
    Without -Destination, writes a table. With -Destination, copies the same set.
.PARAMETER Destination
    Directory to copy into. Created when missing.
.PARAMETER Count
    Newest files kept from each root. Default is 50.
.EXAMPLE
    scripts/collect-logs.ps1
    scripts/collect-logs.ps1 -Destination ./logs-snapshot
    scripts/collect-logs.ps1 -Count 10 -Destination ./logs-snapshot
#>
param(
    [string]$Destination = "",
    [int]$Count = 50
)

$ErrorActionPreference = "Stop"

function Add-MatchingFiles {
    param(
        [System.Collections.Generic.List[System.IO.FileInfo]]$Sink,
        [string]$Root,
        [string]$Label,
        [string]$Filter,
        [switch]$Recurse
    )

    if ([string]::IsNullOrWhiteSpace($Root) -or -not (Test-Path -LiteralPath $Root)) {
        return
    }

    $files = if ($Recurse) {
        Get-ChildItem -LiteralPath $Root -Recurse -File -Filter $Filter -ErrorAction SilentlyContinue
    }
    else {
        Get-ChildItem -LiteralPath $Root -File -Filter $Filter -ErrorAction SilentlyContinue
    }

    foreach ($file in @($files | Sort-Object LastWriteTime -Descending | Select-Object -First $Count)) {
        $file | Add-Member -NotePropertyName Source -NotePropertyValue $Label -Force
        $Sink.Add($file)
    }
}

$collected = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
$appData = Join-Path $env:APPDATA "RevitDevTool"
Add-MatchingFiles $collected $appData "RevitDevTool" "log_*" -Recurse
Add-MatchingFiles $collected $appData "RevitDevTool" "crash_*" -Recurse

$seenFolders = @{}
if (Test-Path -LiteralPath $appData) {
    $seenFolders[$appData] = $true
    foreach ($config in @(Get-ChildItem -LiteralPath $appData -Recurse -Filter LogConfig.json -File -ErrorAction SilentlyContinue)) {
        $folder = $null
        try {
            $json = Get-Content -LiteralPath $config.FullName -Raw | ConvertFrom-Json
            $folder = [string]$json.fileLogging.logFolder
        }
        catch {
            Write-Warning "Could not read $($config.FullName)"
            continue
        }

        if ([string]::IsNullOrWhiteSpace($folder) -or $seenFolders.ContainsKey($folder)) {
            continue
        }
        if ($folder.StartsWith($appData, [StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $seenFolders[$folder] = $true
        Add-MatchingFiles $collected $folder "custom" "log_*" -Recurse
        Add-MatchingFiles $collected $folder "custom" "crash_*" -Recurse
    }
}

Add-MatchingFiles $collected $env:TEMP "temp" "RevitDevTool-*.txt"

$revitJournals = Join-Path $env:LOCALAPPDATA "Autodesk\Revit"
if (Test-Path -LiteralPath $revitJournals) {
    foreach ($journals in @(Get-ChildItem -LiteralPath $revitJournals -Recurse -Directory -Filter Journals -ErrorAction SilentlyContinue)) {
        Add-MatchingFiles $collected $journals.FullName "journal" "journal*.txt"
    }
}

$recent = @($collected | Sort-Object LastWriteTime -Descending)

if ($recent.Count -eq 0) {
    Write-Warning "No logs found under $appData, custom LogFolder, %TEMP%\RevitDevTool-*.txt, or Revit Journals."
    exit 0
}

if (-not $Destination) {
    $recent | Select-Object Source, FullName, LastWriteTime, Length
    exit 0
}

if (-not (Test-Path -LiteralPath $Destination)) {
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
}

foreach ($file in $recent) {
    $bucket = Join-Path $Destination $file.Source
    New-Item -ItemType Directory -Force -Path $bucket | Out-Null
    $dest = Join-Path $bucket $file.Name
    if (Test-Path -LiteralPath $dest) {
        $stamp = $file.LastWriteTimeUtc.ToString("yyyyMMddHHmmss")
        $dest = Join-Path $bucket ("{0}_{1}" -f $stamp, $file.Name)
    }
    Copy-Item -LiteralPath $file.FullName -Destination $dest -Force
}

Write-Output "Copied $($recent.Count) files to $Destination"
