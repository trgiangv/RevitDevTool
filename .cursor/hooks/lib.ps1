# Shared helpers for RevitDevTool compile-verify hooks.

$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$script:StateDir = Join-Path $script:RepoRoot '.cursor\hooks-state'
$script:PendingPath = Join-Path $script:StateDir 'pending.json'

function Read-RawHookStdin {
    # Read UTF-8 stdin directly; avoids PowerShell pipeline quirks with -File hooks.
    $stdin = [Console]::OpenStandardInput()
    if ($null -eq $stdin) { return $null }
    $reader = New-Object System.IO.StreamReader($stdin, [System.Text.Encoding]::UTF8, $true)
    try {
        return $reader.ReadToEnd()
    } finally {
        $reader.Dispose()
    }
}

function Write-HookDiag([string]$Message) {
    try {
        Ensure-StateDir
        $line = "[$(Get-Date -Format 'o')] $Message"
        Add-Content -LiteralPath (Join-Path $script:StateDir 'hook.log') -Value $line -Encoding UTF8
    } catch {
        # Best-effort diagnostics only.
    }
}

function Read-HookStdin {
    $raw = Read-RawHookStdin
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return $null
    }

    $raw = $raw.Trim()
    if ($raw.Length -ge 1 -and [int][char]$raw[0] -eq 0xFEFF) {
        $raw = $raw.Substring(1)
    }

    try {
        return ConvertFrom-Json -InputObject $raw
    } catch {
        $preview = if ($raw.Length -gt 300) { $raw.Substring(0, 300) + '...' } else { $raw }
        Write-HookDiag "JSON parse failed: $_ | raw=$preview"
        return $null
    }
}

function Sanitize-HookText([string]$Text) {
    if ([string]::IsNullOrEmpty($Text)) { return $Text }
    # Keep JSON stdout ASCII-safe for Windows PowerShell ConvertTo-Json + Cursor parser.
    return ($Text -replace [char]0x2014, '-' -replace [char]0x2013, '-' -replace [char]0x2192, '->')
}

function Write-HookJson([hashtable]$Object) {
    $payload = @{}
    foreach ($key in $Object.Keys) {
        $value = $Object[$key]
        if ($value -is [string]) {
            $value = Sanitize-HookText $value
        }
        $payload[$key] = $value
    }

    $json = $payload | ConvertTo-Json -Compress -Depth 10
    $utf8 = New-Object System.Text.UTF8Encoding $false
    $bytes = $utf8.GetBytes($json)
    $stdout = [Console]::OpenStandardOutput()
    $stdout.Write($bytes, 0, $bytes.Length)
}

function Ensure-StateDir {
    if (-not (Test-Path -LiteralPath $script:StateDir)) {
        New-Item -ItemType Directory -Path $script:StateDir -Force | Out-Null
    }
}

function Read-PendingState {
    Ensure-StateDir
    if (-not (Test-Path -LiteralPath $script:PendingPath)) {
        return [pscustomobject]@{ files = @(); mode = 'Debug' }
    }
    try {
        $state = Get-Content -LiteralPath $script:PendingPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -eq $state.files) { $state | Add-Member -NotePropertyName files -NotePropertyValue @() -Force }
        if ([string]::IsNullOrWhiteSpace([string]$state.mode)) { $state.mode = 'Debug' }
        return $state
    } catch {
        return [pscustomobject]@{ files = @(); mode = 'Debug' }
    }
}

function Write-PendingState($State) {
    Ensure-StateDir
    $payload = @{
        files = @($State.files | Select-Object -Unique)
        mode  = if ($State.mode) { [string]$State.mode } else { 'Debug' }
    }
    ($payload | ConvertTo-Json -Compress -Depth 5) | Set-Content -LiteralPath $script:PendingPath -Encoding UTF8 -NoNewline
}

function Clear-PendingState {
    if (Test-Path -LiteralPath $script:PendingPath) {
        Remove-Item -LiteralPath $script:PendingPath -Force -ErrorAction SilentlyContinue
    }
}

function Find-ProjectFile([string]$FilePath) {
    if (-not $FilePath) { return $null }
    if ($FilePath -match '\.csproj$') { return $FilePath }

    $dir = Split-Path -Parent $FilePath
    while ($dir -and ($dir.Length -ge $script:RepoRoot.Length)) {
        $hit = Get-ChildItem -LiteralPath $dir -Filter '*.csproj' -File -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($hit) { return $hit.FullName }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    return $null
}

function Get-ProjectKind([string]$CsprojPath) {
    $text = Get-Content -LiteralPath $CsprojPath -Raw -ErrorAction SilentlyContinue
    if (-not $text) { return 'simple' }
    if ($text -match '<UseRevit>\s*true\s*</UseRevit>') { return 'revit' }
    if ($text -match '<UseAutoCad>\s*true\s*</UseAutoCad>') { return 'autocad' }
    if ($text -match '<TargetFrameworks>') { return 'multitf' }
    return 'simple'
}

function Resolve-HookFilePath($Hook) {
    if ($null -eq $Hook) { return $null }

    $filePath = [string]$Hook.file_path
    if ([string]::IsNullOrWhiteSpace($filePath)) { return $null }

    if (-not [System.IO.Path]::IsPathRooted($filePath)) {
        $roots = @($Hook.workspace_roots)
        foreach ($root in $roots) {
            if ([string]::IsNullOrWhiteSpace($root)) { continue }
            $candidate = Join-Path $root $filePath
            if (Test-Path -LiteralPath $candidate) {
                return [System.IO.Path]::GetFullPath($candidate)
            }
        }
        $filePath = Join-Path $script:RepoRoot $filePath
    }

    return [System.IO.Path]::GetFullPath($filePath)
}

function Test-IsTrackablePath([string]$FilePath) {
    if (-not $FilePath) { return $false }
    $full = [System.IO.Path]::GetFullPath($FilePath)
    if (-not $full.StartsWith($script:RepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }
    $rel = $full.Substring($script:RepoRoot.Length).TrimStart('\', '/')
    if ($rel -match '(^|[/\\])(bin|obj|\.git|\.cursor[/\\]hooks-state)([/\\]|$)') { return $false }
    if ($rel -notmatch '\.(cs|csproj|xaml|fs)$') { return $false }
    if ($rel -notmatch '^(source|build|tests|install)([/\\]|$)') { return $false }
    return $true
}

function Get-BuildPlan([string]$CsprojPath, [string]$Mode) {
    $kind = Get-ProjectKind $CsprojPath
    $mode = if ($Mode -eq 'Release') { 'Release' } else { 'Debug' }
    $deployOff = @(
        '-p:DeployRevitAddin=false',
        '-p:DeployAutoCadBundle=false',
        '-p:IsRepackable=false'
    )

    $configs = @()
    switch ($kind) {
        'revit' { $configs = @("$mode.Autodesk.2022", "$mode.Autodesk.2025", "$mode.Autodesk.2027") }
        'autocad' { $configs = @("$mode.Autodesk.2022", "$mode.Autodesk.2025", "$mode.Autodesk.2027") }
        default { $configs = @($mode) }
    }

    return [pscustomobject]@{
        Kind     = $kind
        Configs  = $configs
        ExtraArgs = $deployOff
    }
}

function Invoke-ProjectBuilds([string]$CsprojPath, [string]$Mode) {
    $plan = Get-BuildPlan $CsprojPath $Mode
    $results = @()
    foreach ($config in $plan.Configs) {
        $argList = @(
            'build', $CsprojPath,
            '-c', $config,
            '--nologo',
            '-v', 'q'
        ) + $plan.ExtraArgs

        $output = & dotnet @argList 2>&1 | Out-String
        $code = $LASTEXITCODE
        $errors = @($output -split "`r?`n" | Where-Object { $_ -match 'error\s+(CS|MSB|NETSDK|RZ)' } | Select-Object -First 30)
        $results += [pscustomobject]@{
            Config  = $config
            Kind    = $plan.Kind
            ExitCode = $code
            Ok      = ($code -eq 0)
            Errors  = $errors
            Tail    = (($output -split "`r?`n" | Select-Object -Last 12) -join "`n")
        }
        if ($code -ne 0) { break }
    }
    return $results
}
