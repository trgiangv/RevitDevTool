# Shared helpers for scripts/*.ps1 (repo root is one level above this folder).

$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Assert-RepoRoot {
    $slnx = Join-Path $script:RepoRoot 'RevitDevTool.slnx'
    if (-not (Test-Path -LiteralPath $slnx)) {
        throw "Repo root not found (expected RevitDevTool.slnx at $slnx)."
    }
}

function Join-RepoPath([string]$RelativePath) {
    return Join-Path $script:RepoRoot $RelativePath
}
