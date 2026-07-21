# Queues edited source files for stop-hook compile verification.
. "$PSScriptRoot\lib.ps1"

$hook = Read-HookStdin
if ($null -eq $hook) {
    Write-HookJson @{}
    exit 0
}

$filePath = Resolve-HookFilePath $hook
if (-not (Test-IsTrackablePath $filePath)) {
    Write-HookJson @{}
    exit 0
}

$state = Read-PendingState
$files = @($state.files)
if ($files -notcontains $filePath) {
    $files += $filePath
}
$state.files = $files

# Packaging/build pipeline edits → prefer Release for shared projects.
$rel = $filePath.Substring($script:RepoRoot.Length).TrimStart('\', '/')
if ($rel -match '^(build|install)([/\\]|$)') {
    $state.mode = 'Release'
}

Write-PendingState $state
Write-HookJson @{}
exit 0
