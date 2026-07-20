<#
.SYNOPSIS
    Run .NET test projects.
.DESCRIPTION
    Without -Project: runs all three test projects (Execution, Server, Telemetry) sequentially.
    With -Project: runs only the specified test project.
    Stops on first failure.
.PARAMETER Project
    Relative path to a specific test .csproj (e.g. tests/RevitDevTool.Execution.Tests/RevitDevTool.Execution.Tests.csproj).
.PARAMETER Configuration
    Build configuration for tests (default: Debug). The server fixture maps generic Debug/Release to Autodesk 2027.
.EXAMPLE
    scripts/test-dotnet.ps1
    scripts/test-dotnet.ps1 -Project tests/RevitDevTool.Execution.Tests/RevitDevTool.Execution.Tests.csproj
#>
param(
    [string]$Project = "",
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$serverProject = [IO.Path]::GetFullPath((Join-Path $repoRoot "tests/RevitDevTool.Server.Tests/RevitDevTool.Server.Tests.csproj"))

function Resolve-TestTarget([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) {
        return [IO.Path]::GetFullPath($Path)
    }

    return [IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
}

function Get-TestConfiguration([string]$Target) {
    $normalizedTarget = [IO.Path]::GetFullPath($Target)
    if ($normalizedTarget.Equals($serverProject, [StringComparison]::OrdinalIgnoreCase)) {
        if ($Configuration -eq "Debug") { return "Debug.Autodesk.2027" }
        if ($Configuration -eq "Release") { return "Release.Autodesk.2027" }
    }

    return $Configuration
}

if ($Project) {
    $target = Resolve-TestTarget $Project
    dotnet test $target -c (Get-TestConfiguration $target)
    exit $LASTEXITCODE
}

$projects = @(
    "tests/RevitDevTool.Execution.Tests/RevitDevTool.Execution.Tests.csproj",
    "tests/RevitDevTool.Server.Tests/RevitDevTool.Server.Tests.csproj",
    "tests/DevTools.Telemetry.Tests/DevTools.Telemetry.Tests.csproj"
)

foreach ($relative in $projects) {
    $target = Resolve-TestTarget $relative
    dotnet test $target -c (Get-TestConfiguration $target)
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
