<#
.SYNOPSIS
    Runs the Model Context live tests (L2, and L3 with -Ui) in one command and checks that Revit stayed safe.

.DESCRIPTION
    The sample must already be the active model in the Revit given by -RevitPid.
    Open it through MCP, not by hand or UI Automation: launch_host(hostApp="Revit",
    versionNumber="<year>") without a file (it answers the startup dialogs), then
    code_mode open_document with the sample path. A model opened at startup opens
    before the add-in registers its panes, and on Revit 2024 the panes are then never
    created. A document is never opened inside a test run.

    Revit draws a view opened inside a test run only after that run returns, so
    L2 first runs ViewSetupTests.PreparesSample in a run of its own (it activates
    the sample view and shows the Model Context pane through the Revit API; when it
    had to switch the view it ends Inconclusive and runs again, up to three times; the
    script goes on only once it passed, else it exits with code 4), then
    the full suite in a second run on the same Revit. L2 works through the Revit
    API only: it takes no mouse, keyboard or front window, so Revit can stay behind
    other windows while you work. The visibility cases export view images only for
    a failing case; -Images exports the full set.

    L3 runs only with -Ui. It attaches to the Revit that L2 recorded and drives the
    panel through UI Automation, so it uses the screen: every UIA action brings the
    Revit window to the front, and UI-18 to UI-20 focus a row and press Shift+F10.
    Do not work at the machine during -Ui.

    Safety: before the run the script notes the Revit journal position and the crash
    dump files. After L2 and after L3 it checks that the Revit process is alive and
    responding, that the journal text written since the start holds no fatal marker
    (ExceptionCode=0x, unhandledExceptionFilter, TaskDialog_Fatal_Error_Occured,
    "fatal error", "unrecoverable error", "Unhandled exception") and that no new dump
    appeared in the journal folder or %LOCALAPPDATA%\CrashDumps. After L3 it also
    runs SafetyEndTests.EndStateIsUnchanged in the host (no document modified, no
    temporary mode left). The result is safety.json in the run folder, and a
    violation makes the script exit with code 3.

    -Filter passes a test filter to the L2 full run (for example UserActionTests or
    ReloadedLinkMatchesOracle) and skips L3. Explicit tests run only when their own
    method name is the filter.

.EXAMPLE
    scripts/test-models-context.ps1 -RevitPid 12345
    scripts/test-models-context.ps1 -RevitPid 12345 -Ui
    scripts/test-models-context.ps1 -RevitPid 12345 -Year 2026 -Images
    scripts/test-models-context.ps1 -RevitPid 12345 -Year 2026 -Filter UserActionTests
#>
param(
    [Parameter(Mandatory)]
    [int]$RevitPid,
    [string]$Year = '2024',
    [switch]$Ui,
    [switch]$Images,
    [string]$Filter
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$live = Join-Path $root 'tests\RevitDevTool.ModelsContext.LiveTests\RevitDevTool.ModelsContext.LiveTests.csproj'
$uiProject = Join-Path $root 'tests\RevitDevTool.ModelsContext.UiTests\RevitDevTool.ModelsContext.UiTests.csproj'
$runs = Join-Path $root 'artifacts\models-context-tests'
$flag = Join-Path $runs 'images.on'
New-Item -ItemType Directory -Force $runs | Out-Null
if ($Images) { New-Item -ItemType File -Force $flag | Out-Null } else { Remove-Item $flag -ErrorAction SilentlyContinue }

$process = Get-Process -Id $RevitPid -ErrorAction SilentlyContinue
if ($null -eq $process -or $process.ProcessName -ne 'Revit') {
    throw "No Revit runs with pid $RevitPid. Start it with MCP launch_host and pass the pid it returns."
}

$running = $null
try { if ($process.MainModule.FileName -match 'Revit (\d{4})') { $running = $Matches[1] } } catch { $running = $null }
if ($running) {
    if (-not $PSBoundParameters.ContainsKey('Year')) {
        $Year = $running
    } elseif ($running -ne $Year) {
        throw "Revit pid $RevitPid is Revit $running but -Year is $Year."
    }
}

$configuration = "Debug.Autodesk.$Year"
$journalFolder = Join-Path $env:LOCALAPPDATA "Autodesk\Revit\Autodesk Revit $Year\Journals"
$dumpFolders = @($journalFolder, (Join-Path $env:LOCALAPPDATA 'CrashDumps'))
$markers = 'ExceptionCode=0x|unhandledExceptionFilter|TaskDialog_Fatal_Error_Occured|fatal error|unrecoverable error|Unhandled exception'
$phases = @()

function Get-DumpNames {
    foreach ($folder in $dumpFolders) {
        if (Test-Path $folder) {
            Get-ChildItem $folder -Filter '*.dmp' -File | Where-Object { $_.Name -like 'journal.*' -or $_.Name -like 'Revit.exe.*' } | ForEach-Object { $_.FullName }
        }
    }
}

function Get-RevitJournal {
    if (-not (Test-Path $journalFolder)) { return $null }
    $files = Get-ChildItem $journalFolder -Filter 'journal.*.txt' -File | Where-Object { $_.Name -match '^journal\.\d+\.txt$' }
    $own = $files | Where-Object { [math]::Abs(($_.CreationTime - $process.StartTime).TotalMinutes) -lt 5 } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($own) { return $own }
    return $files | Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

$journal = Get-RevitJournal
$journalStart = if ($journal) { $journal.Length } else { 0 }
$dumpsBefore = @(Get-DumpNames)

function Read-JournalSince {
    if (-not $journal) { return '' }
    $stream = [System.IO.FileStream]::new($journal.FullName, 'Open', 'Read', 'ReadWrite, Delete')
    try {
        if ($stream.Length -lt $journalStart) { $stream.Position = 0 } else { $stream.Position = $journalStart }
        return [System.IO.StreamReader]::new($stream).ReadToEnd()
    } finally {
        $stream.Dispose()
    }
}

function Test-Safety([string]$phase) {
    $alive = $false
    $responding = $false
    $current = Get-Process -Id $RevitPid -ErrorAction SilentlyContinue
    if ($current -and $current.StartTime -eq $process.StartTime) {
        $alive = $true
        for ($try = 0; $try -lt 10 -and -not $responding; $try++) {
            $current.Refresh()
            $responding = $current.Responding
            if (-not $responding) { Start-Sleep -Seconds 1 }
        }
    }

    $fatal = @(((Read-JournalSince) -split "`n") | Where-Object { $_ -match $markers } | ForEach-Object { $_.Trim() } | Select-Object -First 5)
    $dumps = @(Get-DumpNames | Where-Object { $dumpsBefore -notcontains $_ })
    $ok = $alive -and $responding -and $fatal.Count -eq 0 -and $dumps.Count -eq 0
    $result = [ordered]@{
        phase = $phase
        at = (Get-Date).ToString('s')
        alive = $alive
        responding = $responding
        journal = if ($journal) { $journal.FullName } else { $null }
        fatalLines = $fatal
        newDumps = $dumps
        ok = $ok
    }
    $script:phases += [pscustomobject]$result
    Save-Safety
    if ($ok) { Write-Host "Safety $phase : ok" } else { Write-Host "Safety $phase : VIOLATION $($result | ConvertTo-Json -Compress)" -ForegroundColor Red }
    return $ok
}

function Get-RunFolder {
    $latest = Join-Path $runs 'latest.txt'
    if (Test-Path $latest) {
        $folder = Join-Path $runs ((Get-Content $latest -Raw).Trim())
        if (Test-Path $folder) { return $folder }
    }
    return $runs
}

function Save-Safety {
    $all = ($script:phases | Where-Object { -not $_.ok }).Count -eq 0
    $json = [ordered]@{ revitPid = $RevitPid; year = $Year; ok = $all; phases = $script:phases } | ConvertTo-Json -Depth 5
    Set-Content -Path (Join-Path (Get-RunFolder) 'safety.json') -Value $json -Encoding UTF8
}

function Complete([int]$testExit) {
    $safe = ($phases | Where-Object { -not $_.ok }).Count -eq 0
    if ($testExit -ne 0) { exit $testExit }
    if (-not $safe) { exit 3 }
    exit 0
}

Write-Host "Revit pid $RevitPid, configuration $configuration"
$pidArgs = @('--pid', $RevitPid)

$prepared = $false
foreach ($attempt in 1..3) {
    $lines = @(dotnet test --project $live -c $configuration -- @pidArgs --filter PreparesSample 2>&1)
    $code = $LASTEXITCODE
    $lines | ForEach-Object { Write-Host $_ }
    if ($code -eq 0 -and ($lines -match '^\s*succeeded:\s*1\b')) { $prepared = $true; break }
    Write-Host "PreparesSample did not pass on attempt $attempt (it ends Inconclusive after it switches the view); running it again."
}
if (-not $prepared) {
    Write-Host 'PreparesSample never passed: the sample view or the Model Context pane is not ready. See its message above.' -ForegroundColor Red
    exit 4
}

$filterArgs = if ($Filter) { @('--filter', $Filter) } else { @() }
dotnet test --project $live -c $configuration --no-build -- @pidArgs @filterArgs
$l2 = $LASTEXITCODE
$l2Safe = Test-Safety 'after-L2'
if ($l2 -ne 0 -or -not $l2Safe) { Complete $l2 }

if (-not $Ui -or $Filter) {
    Complete 0
}

$env:MODELS_CONTEXT_REVIT_PID = $RevitPid
dotnet run --project $uiProject
$l3 = $LASTEXITCODE
$l3Safe = Test-Safety 'after-L3'

dotnet test --project $live -c $configuration --no-build -- @pidArgs --filter EndStateIsUnchanged
$end = $LASTEXITCODE
$phases += [pscustomobject]@{ phase = 'after-L3 host end state'; at = (Get-Date).ToString('s'); ok = ($end -eq 0); testExit = $end }
Save-Safety
if ($end -ne 0) { Write-Host 'Safety after-L3 host end state: VIOLATION (see safety-host-end.json)' -ForegroundColor Red }

Complete $l3
