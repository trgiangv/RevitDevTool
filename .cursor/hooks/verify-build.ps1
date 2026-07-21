# On agent stop: compile queued projects and follow up only on failure.
. "$PSScriptRoot\lib.ps1"

$hook = Read-HookStdin
if ($null -eq $hook) {
    Write-HookJson @{}
    exit 0
}

$status = [string]$hook.status
$loopCount = 0
if ($null -ne $hook.loop_count) { $loopCount = [int]$hook.loop_count }

if ($status -ne 'completed') {
    Write-HookJson @{}
    exit 0
}

$state = Read-PendingState
$files = @($state.files | Where-Object { $_ })
if ($files.Count -eq 0) {
    Write-HookJson @{}
    exit 0
}

$projects = @()
foreach ($file in $files) {
    $proj = Find-ProjectFile $file
    if ($proj -and ($projects -notcontains $proj)) {
        $projects += $proj
    }
}

if ($projects.Count -eq 0) {
    Clear-PendingState
    Write-HookJson @{}
    exit 0
}

$mode = if ($state.mode -eq 'Release') { 'Release' } else { 'Debug' }
$failures = @()
$summaries = @()

foreach ($proj in $projects) {
    $relProj = $proj.Substring($script:RepoRoot.Length).TrimStart('\', '/')
    $results = Invoke-ProjectBuilds -CsprojPath $proj -Mode $mode
    foreach ($r in $results) {
        $summaries += "- $relProj [$($r.Kind)] $($r.Config): $(if ($r.Ok) { 'OK' } else { 'FAIL' })"
        if (-not $r.Ok) {
            $errBlock = if ($r.Errors -and $r.Errors.Count -gt 0) {
                ($r.Errors -join "`n")
            } else {
                $r.Tail
            }
            $failures += @"
Project: $relProj
Config: $($r.Config) (kind=$($r.Kind))
Exit: $($r.ExitCode)
Errors:
$errBlock
"@
            break
        }
    }
    if ($failures.Count -gt 0) { break }
}

if ($failures.Count -eq 0) {
    Clear-PendingState
    Write-HookJson @{}
    exit 0
}

# Keep pending so a follow-up fix re-verifies the same set.
$failText = ($failures -join "`n`n")
if ($failText.Length -gt 6000) {
    $failText = $failText.Substring(0, 6000) + "`n...(truncated)"
}

$summary = ($summaries -join "`n")
$followup = @"
Compile verification failed after your edits (hook loop $loopCount). Fix the build errors below - do not re-read a build skill. Prefer the smallest code fix; the stop hook will re-run compile-only builds automatically.

Plans: Revit/AutoCAD API projects -> $mode.Autodesk.2022/2025/2027; shared/multi-TFM -> $mode. Deploy is OFF.

Results:
$summary

$failText
"@

Write-HookJson @{ followup_message = $followup }
exit 0
