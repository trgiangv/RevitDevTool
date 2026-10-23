param(
    [string[]] $Paths,
    [switch] $All
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$scopes = @('source', 'tests', 'build', 'install')
$hostConfiguration = 'Debug.Autodesk.2025'
$stylerConfig = Join-Path $repoRoot 'Settings.XamlStyler'
Set-Location $repoRoot

function Invoke-Dotnet([string[]] $Arguments) {
    $output = & dotnet @Arguments 2>&1 | ForEach-Object { "$_".Trim() } | Where-Object { $_ } | Select-Object -Unique
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0..1] -join ' ') failed (exit $LASTEXITCODE):`n$($output -join "`n")" }
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
    throw "No .csproj found for '$file'."
}

if ($All) {
    $include = $scopes | ForEach-Object { "$_/" }
    $remaining = [Collections.Generic.List[string]]::new()
    try {
        Get-ChildItem $scopes -Filter '*.xaml' -Recurse -File | Where-Object FullName -notmatch '\\(bin|obj)\\' | ForEach-Object { Format-Xaml $_.FullName }
        foreach ($configuration in @($hostConfiguration, $null)) {
            $env:Configuration = $configuration
            Invoke-Dotnet @('restore', 'RevitDevTool.slnx')
            Invoke-Dotnet (@('format', 'whitespace', 'RevitDevTool.slnx', '--include') + $include + @('--exclude', 'libs/', '--no-restore'))
            Invoke-Dotnet (@('format', 'style', 'RevitDevTool.slnx', '--include') + $include + @('--exclude', 'libs/', '--severity', 'warn', '--no-restore'))
            & dotnet format style RevitDevTool.slnx --include @include --exclude libs/ --severity warn --no-restore --verify-no-changes 2>&1 |
                Select-String '(error|warning) [A-Z]+\d+' | ForEach-Object { $remaining.Add(($_.Line -replace '\s*\[[^\]]+\]$', '').Trim()) }
        }
    } catch {
        [Console]::Error.WriteLine($_.Exception.Message)
        exit 2
    } finally {
        $env:Configuration = $null
    }

    $remaining = @($remaining | Sort-Object -Unique)
    if ($remaining) {
        [Console]::Error.WriteLine("Formatted, but $($remaining.Count) issue(s) need a manual fix:`n$($remaining -join "`n")")
        exit 2
    }
    Write-Output "Formatted all .cs/.xaml under $($include -join ', ')"
    exit 0
}

if (-not $Paths) {
    if (-not [Console]::IsInputRedirected) {
        Write-Output 'Usage: format-lint-hook.ps1 -Paths <file>[,...] | -All. Without arguments it reads a hook event JSON from stdin.'
        exit 0
    }
    $toolInput = ([Console]::In.ReadToEnd() | ConvertFrom-Json).tool_input
    $patchText = (@($toolInput.input) + @($toolInput.patch) + @($toolInput.command)) -join "`n"
    $patchPaths = [regex]::Matches($patchText, '(?m)^\*\*\* (?:Update|Add) File: (.+?)\s*$') | ForEach-Object { $_.Groups[1].Value }
    $Paths = @($toolInput.file_path, $toolInput.path) + $patchPaths | Where-Object { $_ }
}

$files = foreach ($path in $Paths) {
    $full = [IO.Path]::GetFullPath($path, $repoRoot)
    $relative = [IO.Path]::GetRelativePath($repoRoot, $full).Replace('\', '/')
    if ($relative -notmatch "^($($scopes -join '|'))/") { continue }
    if ($relative -match '/(bin|obj)/') { continue }
    if ($full -notmatch '\.(cs|xaml)$' -or -not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }
    $relative
}
if (-not $files) {
    Write-Output "Nothing to format: no .cs/.xaml file under $($scopes -join '/, ')/."
    exit 0
}

try {
    foreach ($file in $files) {
        if ($file.EndsWith('.xaml')) {
            Format-Xaml (Join-Path $repoRoot $file)
            continue
        }
        $project = Find-Project $file
        $isHost = (Get-Content -LiteralPath $project -Raw) -match '<Use(Revit|AutoCad)>\s*true'
        $env:Configuration = if ($isHost) { $hostConfiguration } else { $null }
        $restore = if ($isHost) { @() } else { @('--no-restore') }
        Invoke-Dotnet (@('format', 'whitespace', $project, '--include', $file) + $restore)
        Invoke-Dotnet @('format', 'style', $project, '--include', $file, '--severity', 'warn', '--no-restore')
        Invoke-Dotnet @('format', 'style', $project, '--include', $file, '--severity', 'warn', '--no-restore', '--verify-no-changes')
    }
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 2
} finally {
    $env:Configuration = $null
}

Write-Output "Formatted: $($files -join ', ')"
