<#
.SYNOPSIS
    Runs the Model Context live tests (L2, then L3) in one command.

.DESCRIPTION
    Without -RevitPid the script starts Revit 2026 with the Snowdon HVAC sample as
    its argument, answers the add-in security dialog with Always Load and waits for
    the sample to be the active model. With -RevitPid the sample must already be
    open in that Revit. A document is never opened inside a test run.

    Revit draws a view opened inside a test run only after that run returns, so
    L2 first runs ViewSetupTests.OpensSampleView in a run of its own, then the
    full suite in a second run on the same Revit. L3 follows and attaches to the
    Revit that L2 recorded. The visibility cases export view images only for a
    failing case; -Images exports the full set.

.EXAMPLE
    scripts/test-models-context.ps1
    scripts/test-models-context.ps1 -RevitPid 12345 -SkipUi
    scripts/test-models-context.ps1 -Images
#>
param(
    [int]$RevitPid,
    [string]$Configuration = 'Debug.Autodesk.2026',
    [switch]$SkipUi,
    [switch]$Images,
    [int]$StartTimeout = 300
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$live = Join-Path $root 'tests\RevitDevTool.ModelsContext.LiveTests\RevitDevTool.ModelsContext.LiveTests.csproj'
$ui = Join-Path $root 'tests\RevitDevTool.ModelsContext.UiTests\RevitDevTool.ModelsContext.UiTests.csproj'
$uia = Join-Path $root '.agents\skills\ui-automation\scripts\uia.ps1'
$revit = 'C:\Program Files\Autodesk\Revit 2026\Revit.exe'
$sample = 'C:\Program Files\Autodesk\Revit 2026\Samples\Snowdon Towers Sample HVAC.rvt'
$title = 'Snowdon Towers Sample HVAC'
$flag = Join-Path $root 'artifacts\models-context-tests\images.on'
New-Item -ItemType Directory -Force (Split-Path $flag) | Out-Null
if ($Images) { New-Item -ItemType File -Force $flag | Out-Null } else { Remove-Item $flag -ErrorAction SilentlyContinue }

if ($RevitPid -le 0) {
    $process = Start-Process $revit -ArgumentList "`"$sample`"" -PassThru
    $RevitPid = $process.Id
    $deadline = [DateTime]::UtcNow.AddSeconds($StartTimeout)
    while ($true) {
        $process.Refresh()
        if ($process.HasExited) { throw "Revit exited while starting (pid $RevitPid)." }
        if ($process.MainWindowTitle -like "*$title*") { break }
        if ([DateTime]::UtcNow -gt $deadline) { throw "The sample was not the active model within $StartTimeout s (pid $RevitPid)." }
        pwsh -File $uia dialog -ProcessId $RevitPid -Title 'Security*' -Button 'Always Load' *> $null
        Start-Sleep -Milliseconds 500
    }
}

Write-Host "Revit pid $RevitPid"
$pidArgs = @('--pid', $RevitPid)

dotnet test --project $live -c $Configuration -- @pidArgs --filter OpensSampleView
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test --project $live -c $Configuration --no-build -- @pidArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipUi) {
    dotnet run --project $ui
    exit $LASTEXITCODE
}
