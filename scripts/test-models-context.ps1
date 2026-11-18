<#
.SYNOPSIS
    Runs the Model Context live tests (L2, then L3) in one command.

.DESCRIPTION
    The sample must already be the active model in the Revit given by -RevitPid.
    Open it through MCP, not by hand or UI Automation: launch_host(hostApp="Revit",
    versionNumber="<year>") without a file (it answers the startup dialogs), then
    code_mode open_document with the sample path. A model opened at startup opens
    before the add-in registers its panes, and on Revit 2024 the panes are then never
    created. A document is never opened inside a test run.

    Revit draws a view opened inside a test run only after that run returns, so
    L2 first runs ViewSetupTests.PreparesSample in a run of its own (it activates
    the sample view and shows the Model Context pane through the Revit API), then
    the full suite in a second run on the same Revit. L3 follows and attaches to
    the Revit that L2 recorded; it uses UI Automation only for the panel's own
    controls. The visibility cases export view images only for a failing case;
    -Images exports the full set.

.EXAMPLE
    scripts/test-models-context.ps1 -RevitPid 12345
    scripts/test-models-context.ps1 -RevitPid 12345 -SkipUi
    scripts/test-models-context.ps1 -RevitPid 12345 -Year 2026 -Images
#>
param(
    [Parameter(Mandatory)]
    [int]$RevitPid,
    [string]$Year = '2024',
    [switch]$SkipUi,
    [switch]$Images
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$live = Join-Path $root 'tests\RevitDevTool.ModelsContext.LiveTests\RevitDevTool.ModelsContext.LiveTests.csproj'
$ui = Join-Path $root 'tests\RevitDevTool.ModelsContext.UiTests\RevitDevTool.ModelsContext.UiTests.csproj'
$configuration = "Debug.Autodesk.$Year"
$flag = Join-Path $root 'artifacts\models-context-tests\images.on'
New-Item -ItemType Directory -Force (Split-Path $flag) | Out-Null
if ($Images) { New-Item -ItemType File -Force $flag | Out-Null } else { Remove-Item $flag -ErrorAction SilentlyContinue }

$process = Get-Process -Id $RevitPid -ErrorAction SilentlyContinue
if ($null -eq $process -or $process.ProcessName -ne 'Revit') {
    throw "No Revit runs with pid $RevitPid. Start it with MCP launch_host and pass the pid it returns."
}

Write-Host "Revit pid $RevitPid, configuration $configuration"
$pidArgs = @('--pid', $RevitPid)

dotnet test --project $live -c $configuration -- @pidArgs --filter PreparesSample
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test --project $live -c $configuration --no-build -- @pidArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $SkipUi) {
    $env:MODELS_CONTEXT_REVIT_PID = $RevitPid
    dotnet run --project $ui
    exit $LASTEXITCODE
}
