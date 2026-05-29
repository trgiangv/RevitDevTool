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
