param(
    [string]$Project = "",
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "../..")

if ($Project) {
    $target = Join-Path $repoRoot $Project
    dotnet test $target -c $Configuration
    exit $LASTEXITCODE
}

$projects = @(
    "tests/RevitDevTool.Execution.Tests/RevitDevTool.Execution.Tests.csproj",
    "tests/RevitDevTool.Server.Tests/RevitDevTool.Server.Tests.csproj",
    "tests/DevTools.Telemetry.Tests/DevTools.Telemetry.Tests.csproj"
)

foreach ($relative in $projects) {
    $target = Join-Path $repoRoot $relative
    dotnet test $target -c $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
