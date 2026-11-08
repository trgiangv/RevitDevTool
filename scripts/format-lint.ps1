param(
    [string[]] $Paths,
    [switch] $All
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$scopes = @('source', 'tests', 'build')
$hostConfiguration = 'Debug.Autodesk.2025'
$stylerConfig = Join-Path $repoRoot 'Settings.XamlStyler'
$remaining = [Collections.Generic.List[string]]::new()
Set-Location $repoRoot

function Invoke-Dotnet([string[]] $Arguments) {
    $output = & dotnet @Arguments 2>&1 | ForEach-Object { "$_".Trim() } | Where-Object { $_ } | Select-Object -Unique
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0..1] -join ' ') failed (exit $LASTEXITCODE):`n$($output -join "`n")" }
}

function Add-Remaining([string[]] $Arguments) {
    & dotnet @Arguments 2>&1 | Select-String '(error|warning) [A-Z]+\d+' |
        ForEach-Object { $remaining.Add(($_.Line -replace '\s*\[[^\]]+\]$', '').Trim()) }
}

function Format-Xaml([string] $file) {
    Invoke-Dotnet @('tool', 'run', 'xstyler', '--', '--file', $file, '--config', $stylerConfig)
    $text = [IO.File]::ReadAllText($file).TrimStart([char] 0xFEFF).Replace("`r`n", "`n")
    [IO.File]::WriteAllText($file, $text, [Text.UTF8Encoding]::new($false))
}

function Find-Project([string] $file) {
    $dir = Split-Path -Parent (Join-Path $repoRoot $file)
    while ($dir -and $dir.Length -ge $repoRoot.Length) {
        $project = Get-ChildItem -LiteralPath $dir -Filter '*.csproj' -File | Select-Object -First 1
        if ($project) { return $project.FullName }
        $dir = Split-Path -Parent $dir
    }
    $null
}

function Get-ChangedPaths {
    git status --porcelain --untracked-files=all | ForEach-Object {
        $path = $_.Substring(3)
        if ($path -match ' -> (.+)$') { $path = $Matches[1] }
        $path.Trim('"')
    }
}

try {
    if ($All) {
        $include = $scopes | ForEach-Object { "$_/" }
        Get-ChildItem $scopes -Filter '*.xaml' -Recurse -File | Where-Object FullName -notmatch '\\(bin|obj)\\' | ForEach-Object { Format-Xaml $_.FullName }
        foreach ($configuration in @($hostConfiguration, $null)) {
            $env:Configuration = $configuration
            Invoke-Dotnet @('restore', 'RevitDevTool.slnx')
            Invoke-Dotnet (@('format', 'whitespace', 'RevitDevTool.slnx', '--include') + $include + @('--exclude', 'libs/', '--no-restore'))
            Invoke-Dotnet (@('format', 'style', 'RevitDevTool.slnx', '--include') + $include + @('--exclude', 'libs/', '--severity', 'warn', '--no-restore'))
            Add-Remaining (@('format', 'style', 'RevitDevTool.slnx', '--include') + $include + @('--exclude', 'libs/', '--severity', 'warn', '--no-restore', '--verify-no-changes'))
        }
        $done = "all .cs/.xaml under $($include -join ', ')"
    }
    else {
        if (-not $Paths) { $Paths = Get-ChangedPaths }
        $files = foreach ($path in $Paths) {
            $full = [IO.Path]::GetFullPath($path, $repoRoot)
            $relative = [IO.Path]::GetRelativePath($repoRoot, $full).Replace('\', '/')
            if ($relative -notmatch "^($($scopes -join '|'))/" -or $relative -match '/(bin|obj)/') { continue }
            if ($full -notmatch '\.(cs|xaml)$' -or -not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }
            $relative
        }
        if (-not $files) {
            Write-Output "Nothing to format: no changed .cs/.xaml file under $($scopes -join '/, ')/."
            exit 0
        }

        $files | Where-Object { $_.EndsWith('.xaml') } | ForEach-Object { Format-Xaml (Join-Path $repoRoot $_) }
        $csharp = $files | Where-Object { $_.EndsWith('.cs') } | ForEach-Object { [pscustomobject]@{ File = $_; Project = Find-Project $_ } }
        $csharp | Where-Object { -not $_.Project } | ForEach-Object { Write-Output "Skipped $($_.File): no project folder above it." }
        $csharp | Where-Object Project | Group-Object Project | ForEach-Object {
            $project = $_.Name
            $include = @('--include') + $_.Group.File
            $isHost = (Get-Content -LiteralPath $project -Raw) -match '<Use(Revit|AutoCad)>\s*true'
            $env:Configuration = if ($isHost) { $hostConfiguration } else { $null }
            $restore = if ($isHost) { @() } else { @('--no-restore') }
            Invoke-Dotnet (@('format', $project) + $include + @('--severity', 'warn') + $restore)
            Add-Remaining (@('format', 'style', $project) + $include + @('--severity', 'warn', '--no-restore', '--verify-no-changes'))
        }
        $done = $files -join ', '
    }
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
finally {
    $env:Configuration = $null
}

$remaining = @($remaining | Sort-Object -Unique)
if ($remaining) {
    [Console]::Error.WriteLine("Formatted, but $($remaining.Count) issue(s) need a manual fix:`n$($remaining -join "`n")")
    exit 1
}
Write-Output "Formatted: $done"
