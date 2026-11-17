#Requires -Version 7.0
<#
.SYNOPSIS
Find and drive Windows desktop UI through UI Automation (FlaUI UIA3).

.DESCRIPTION
Targets one app by process id, process name, or window title, finds elements by
AutomationId, Name, or ControlType, and acts through UIA patterns. Prints one
line per element. Exit codes: 0 ok, 1 not found or timeout, 2 bad usage or
setup, 3 the element does not support the action, 4 refused for safety.

.EXAMPLE
pwsh -File uia.ps1 windows -ProcessId 1234
pwsh -File uia.ps1 tree -WindowTitle 'My App*' -Depth 4
pwsh -File uia.ps1 invoke -ProcessId 1234 -Id Sandbox.Reload
pwsh -File uia.ps1 select -ProcessId 1234 -Id Sandbox.Delay -Item '100 ms'
pwsh -File uia.ps1 dialog -ProcessId 1234 -Title 'Notepad' -Button 'Cancel'
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0, Mandatory)]
    [ValidateSet('windows', 'tree', 'find', 'invoke', 'toggle', 'select', 'expand', 'collapse',
        'setvalue', 'items', 'wait', 'dialog', 'keys', 'shot')]
    [string]$Action,
    [int]$ProcessId,
    [string]$ProcessName,
    [string]$WindowTitle,
    [string]$Root,
    [string]$Id,
    [string]$Name,
    [string]$ControlType,
    [int]$Index = 0,
    [string]$Value,
    [string]$Item,
    [string]$Title = '*',
    [string]$Button,
    [string]$Keys,
    [string]$Path,
    [int]$Depth = 3,
    [double]$Timeout = 5,
    [switch]$Gone,
    [switch]$Realize,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'

function Fail([int]$code, [string]$message) {
    Write-Output "error: $message"
    exit $code
}

function Import-FlaUI {
    $cache = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
    $restore = 'Restore them with `dotnet restore` on any project that references FlaUI.UIA3.'
    foreach ($package in 'interop.uiautomationclient', 'flaui.core', 'flaui.uia3') {
        $dll = switch ($package) {
            'interop.uiautomationclient' { 'Interop.UIAutomationClient.dll' }
            'flaui.core' { 'FlaUI.Core.dll' }
            'flaui.uia3' { 'FlaUI.UIA3.dll' }
        }
        $versions = Get-ChildItem (Join-Path $cache $package) -Directory -ErrorAction SilentlyContinue |
            Sort-Object { [version]($_.Name -replace '[-+].*$', '') } -Descending
        $found = $null
        foreach ($version in $versions) {
            $lib = Get-ChildItem (Join-Path $version.FullName 'lib') -Directory -ErrorAction SilentlyContinue |
                Where-Object Name -Match '^(net\d+\.\d+(-windows[\d.]*)?|netcoreapp3\.\d|netstandard2\.\d)$' |
                Sort-Object { if ($_.Name -like 'net*-windows*') { 0 } elseif ($_.Name -like 'netcoreapp*') { 1 } else { 2 } },
                { [version](($_.Name -replace '^net(coreapp|standard)?', '') -replace '-.*$', '') } -Descending |
                Where-Object { Test-Path (Join-Path $_.FullName $dll) } |
                Select-Object -First 1
            if ($lib) { $found = Join-Path $lib.FullName $dll; break }
        }
        if (-not $found) { Fail 2 "$dll was not found under $cache\$package. $restore" }
        Add-Type -Path $found
    }
}

Import-FlaUI
$automation = [FlaUI.UIA3.UIA3Automation]::new()
$cf = $automation.ConditionFactory
$desktop = $automation.GetDesktop()
$CT = [FlaUI.Core.Definitions.ControlType]

function Clip([string]$text, [int]$max = 160) {
    if ($null -eq $text) { return '' }
    $flat = ($text -replace '[\r\n\t]+', ' ')
    if ($flat.Length -gt $max) { $flat.Substring(0, $max) + '...' } else { $flat }
}

function Safe([scriptblock]$get) {
    try { & $get } catch { $null }
}

function Get-PatternNames($e) {
    $names = Safe { $e.GetSupportedPatterns() | ForEach-Object { $_.Name -replace 'Pattern$', '' } }
    @($names | Sort-Object -Unique)
}

function Format-Element($e, [string]$indent = '') {
    $patterns = Get-PatternNames $e
    $p = $e.Patterns
    $parts = [System.Collections.Generic.List[string]]::new()
    $parts.Add("$indent$(Safe { $e.ControlType })")
    $parts.Add("id=$(Safe { $e.Properties.AutomationId.ValueOrDefault })")
    $parts.Add("name='$(Clip (Safe { $e.Properties.Name.ValueOrDefault }))'")
    $class = Safe { $e.Properties.ClassName.ValueOrDefault }
    if ($class) { $parts.Add("class=$class") }
    $parts.Add("patterns=$($patterns -join ',')")
    $parts.Add("enabled=$(Safe { $e.Properties.IsEnabled.ValueOrDefault })")
    $parts.Add("offscreen=$(Safe { $e.Properties.IsOffscreen.ValueOrDefault })")
    if ($patterns -contains 'Toggle') { $parts.Add("toggle=$(Safe { $p.Toggle.Pattern.ToggleState.Value })") }
    if ($patterns -contains 'SelectionItem') { $parts.Add("selected=$(Safe { $p.SelectionItem.Pattern.IsSelected.Value })") }
    if ($patterns -contains 'ExpandCollapse') { $parts.Add("expand=$(Safe { $p.ExpandCollapse.Pattern.ExpandCollapseState.Value })") }
    if ($patterns -contains 'Value') { $parts.Add("value='$(Clip (Safe { $p.Value.Pattern.Value.Value }))'") }
    elseif ($patterns -contains 'Selection') { $parts.Add("selection='$(Clip ((Safe { $p.Selection.Pattern.Selection.Value | ForEach-Object { $_.Properties.Name.ValueOrDefault } }) -join ', '))'") }
    if ($patterns -contains 'RangeValue') { $parts.Add("range=$(Safe { $p.RangeValue.Pattern.Value.Value })") }
    if ($patterns -contains 'Window') { $parts.Add("modal=$(Safe { $p.Window.Pattern.IsModal.Value })") }
    $hwnd = Safe { $e.Properties.NativeWindowHandle.ValueOrDefault }
    if ($hwnd -and $hwnd -ne [IntPtr]::Zero) { $parts.Add(('hwnd=0x{0:X}' -f $hwnd.ToInt64())) }
    $r = Safe { $e.BoundingRectangle }
    if ($r) { $parts.Add("rect=$($r.X),$($r.Y),$($r.Width),$($r.Height)") }
    $parts -join ' '
}

function Wait-Until([scriptblock]$probe, [double]$seconds = $Timeout) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    do {
        $result = Safe $probe
        if ($result) { return $result }
        Start-Sleep -Milliseconds 100
    } while ($clock.Elapsed.TotalSeconds -lt $seconds)
    $null
}

function Get-ProcessIds {
    if ($ProcessId) { return @($ProcessId) }
    if ($ProcessName) {
        $ids = @(Get-Process -Name ($ProcessName -replace '\.exe$', '') -ErrorAction SilentlyContinue | ForEach-Object Id)
        if ($ids.Count -eq 0) { Fail 1 "no process named '$ProcessName'." }
        if ($ids.Count -gt 1) { Fail 2 "$($ids.Count) processes named '$ProcessName' (pids $($ids -join ', ')); pass -ProcessId." }
        return $ids
    }
    @()
}

function Assert-Allowed([int[]]$ids) {
    $blocked = 'consent', 'CredentialUIBroker', 'LogonUI', 'CredentialEnrollmentManager'
    foreach ($id in $ids) {
        $proc = Get-Process -Id $id -ErrorAction SilentlyContinue
        if ($proc -and $blocked -contains $proc.ProcessName) {
            Fail 4 "$($proc.ProcessName) is a credential or elevation prompt; the user must answer it."
        }
    }
}

function Find-TopWindows([int[]]$ids) {
    @(Safe { $desktop.FindAllChildren() } | Where-Object {
            (-not $ids -or $ids -contains $_.Properties.ProcessId.ValueOrDefault) -and
            (-not $WindowTitle -or $_.Properties.Name.ValueOrDefault -like $WindowTitle)
        })
}

function Get-TopWindows {
    $ids = Get-ProcessIds
    if (-not $ids -and -not $WindowTitle) { Fail 2 'pass -ProcessId, -ProcessName, or -WindowTitle.' }
    $windows = Wait-Until { $all = Find-TopWindows $ids; if ($all.Count) { , $all } }
    if (-not $windows) {
        $what = @(if ($ids) { "pid $($ids -join ',')" }; if ($WindowTitle) { "title '$WindowTitle'" }) -join ' and '
        Fail 1 "no top-level window for $what."
    }
    Assert-Allowed @($windows | ForEach-Object { $_.Properties.ProcessId.ValueOrDefault } | Sort-Object -Unique)
    @($windows)
}

function Get-AllWindows($tops) {
    $seen = @{}
    foreach ($top in $tops) {
        foreach ($w in @($top) + @(Safe { $top.FindAllDescendants($cf.ByControlType($CT::Window)) })) {
            if (-not $w) { continue }
            $key = (Safe { $w.Properties.RuntimeId.ValueOrDefault }) -join '.'
            if ($seen.ContainsKey($key)) { continue }
            $seen[$key] = $true
            $w
        }
    }
}

function Get-Scopes($tops) {
    if (-not $Root) { return $tops }
    $found = Wait-Until {
        $hits = @(foreach ($t in $tops) { Safe { $t.FindAllDescendants($cf.ByAutomationId($Root)) } })
        if ($hits.Count) { , $hits }
    }
    if (-not $found) { Fail 1 "root id=$Root not found." }
    @($found)
}

function Get-Condition {
    $conditions = @()
    if ($Id) { $conditions += $cf.ByAutomationId($Id) }
    if ($ControlType) {
        try { $type = [Enum]::Parse($CT, $ControlType, $true) } catch { Fail 2 "unknown -ControlType '$ControlType'." }
        $conditions += $cf.ByControlType($type)
    }
    if ($Name -and $Name -notmatch '[\*\?\[]') { $conditions += $cf.ByName($Name) }
    if ($conditions.Count -eq 0) { return $null }
    $c = $conditions[0]
    foreach ($next in $conditions | Select-Object -Skip 1) { $c = $c.And($next) }
    $c
}

function Find-Matches($scopes) {
    $condition = Get-Condition
    if (-not $condition -and -not $Name) { return @() }
    @(foreach ($s in $scopes) {
            $hits = if ($condition) { Safe { $s.FindAllDescendants($condition) } } else { Safe { $s.FindAllDescendants() } }
            foreach ($h in $hits) {
                if ($Name -and -not ($h.Properties.Name.ValueOrDefault -like $Name)) { continue }
                $h
            }
        })
}

function Describe-Query {
    (@(if ($Id) { "id=$Id" }; if ($Name) { "name='$Name'" }; if ($ControlType) { "type=$ControlType" }) -join ' ')
}

function Resolve-One($scopes) {
    if (-not ($Id -or $Name -or $ControlType)) { Fail 2 "$Action needs -Id, -Name, or -ControlType." }
    $hits = Wait-Until { $all = Find-Matches $scopes; if ($all.Count -gt $Index) { , $all } }
    if (-not $hits) { Fail 1 "not found: $(Describe-Query) index=$Index." }
    if ($hits.Count -gt 1) { [Console]::Out.WriteLine("note: $($hits.Count) matches for $(Describe-Query); using index $Index") }
    $hits[$Index]
}

function Need-Pattern($e, [string]$pattern) {
    if ((Get-PatternNames $e) -notcontains $pattern) {
        Fail 3 "$(Describe-Query) has no $pattern pattern (has: $((Get-PatternNames $e) -join ','))."
    }
}

function Write-Tree($e, [int]$level) {
    Format-Element $e ('  ' * $level)
    if ($level -ge $Depth) { return }
    foreach ($child in @(Safe { $e.FindAllChildren() })) { Write-Tree $child ($level + 1) }
}

function Get-Items($container) {
    $types = $CT::ListItem, $CT::TreeItem, $CT::DataItem, $CT::MenuItem, $CT::TabItem
    $c = $cf.ByControlType($types[0])
    foreach ($t in $types | Select-Object -Skip 1) { $c = $c.Or($cf.ByControlType($t)) }
    @(Safe { $container.FindAllDescendants($c) })
}

function Get-RealizedItems($container) {
    if ((Get-PatternNames $container) -notcontains 'ItemContainer') { Fail 3 "$(Describe-Query) has no ItemContainer pattern; -Realize cannot walk it." }
    $pattern = $container.Patterns.ItemContainer.Pattern
    $after = $null
    $guard = 0
    while ($guard++ -lt 100000) {
        $next = Safe { $pattern.FindItemByProperty($after, $null, $null) }
        if (-not $next) { break }
        if ((Get-PatternNames $next) -contains 'VirtualizedItem') { Safe { $next.Patterns.VirtualizedItem.Pattern.Realize() } | Out-Null }
        $next
        $after = $next
    }
}

function Test-Destructive([string]$label) {
    $plain = ($label -replace "[‘’']", '') -replace '&', ''
    $plain -match '^(delete|remove|discard|dont save|overwrite|replace|erase|format|uninstall|reset|clear all|yes to all|end task|force)\b'
}

function Get-Dialogs($tops) {
    $main = $tops[0]
    $mainKey = (Safe { $main.Properties.RuntimeId.ValueOrDefault }) -join '.'
    $candidates = @(Get-AllWindows $tops | Where-Object {
            $key = (Safe { $_.Properties.RuntimeId.ValueOrDefault }) -join '.'
            $key -ne $mainKey -and -not (Safe { $_.Properties.IsOffscreen.ValueOrDefault }) -and
            ($_.Properties.Name.ValueOrDefault -like $Title)
        })
    $strong = @($candidates | Where-Object {
            (Safe { $_.Patterns.Window.PatternOrDefault.IsModal.ValueOrDefault }) -or
            (Safe { $_.Properties.ClassName.ValueOrDefault }) -eq '#32770'
        })
    if ($strong.Count) { $strong } else { $candidates }
}

function Press($button) {
    $patterns = Get-PatternNames $button
    if ($patterns -contains 'Invoke') { $button.Patterns.Invoke.Pattern.Invoke(); return 'invoke' }
    if ($patterns -contains 'LegacyIAccessible') { $button.Patterns.LegacyIAccessible.Pattern.DoDefaultAction(); return 'legacy' }
    Fail 3 "button '$($button.Name)' has no Invoke or LegacyIAccessible pattern."
}

function Send-Text([string]$text) {
    if (-not ('UiaUnicodeInput' -as [type])) {
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class UiaUnicodeInput
{
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort Vk; public ushort Scan; public uint Flags; public uint Time; public IntPtr Extra; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] struct INPUT { [FieldOffset(0)] public uint Type; [FieldOffset(8)] public KEYBDINPUT Key; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
    public static void Type(string text)
    {
        foreach (var c in text)
        {
            var down = new INPUT { Type = 1, Key = new KEYBDINPUT { Scan = c, Flags = 4 } };
            var up = new INPUT { Type = 1, Key = new KEYBDINPUT { Scan = c, Flags = 4 | 2 } };
            SendInput(2, new[] { down, up }, Marshal.SizeOf<INPUT>());
            System.Threading.Thread.Sleep(5);
        }
    }
}
"@
    }
    [UiaUnicodeInput]::Type($text)
}
function ConvertTo-Keys([string]$chord) {
    $alias = @{ ctrl = 'CONTROL'; control = 'CONTROL'; alt = 'ALT'; shift = 'SHIFT'; win = 'LWIN'; enter = 'RETURN'; esc = 'ESCAPE'
        escape = 'ESCAPE'; del = 'DELETE'; backspace = 'BACK'; pgup = 'PRIOR'; pgdn = 'NEXT'; space = 'SPACE'; tab = 'TAB'
        up = 'UP'; down = 'DOWN'; left = 'LEFT'; right = 'RIGHT'; home = 'HOME'; end = 'END'
    }
    foreach ($token in $chord -split '\+') {
        $t = $token.Trim()
        $nameOf = if ($alias.ContainsKey($t.ToLowerInvariant())) { $alias[$t.ToLowerInvariant()] }
        elseif ($t -match '^[A-Za-z0-9]$') { 'KEY_' + $t.ToUpperInvariant() }
        else { $t.ToUpperInvariant() }
        try { [Enum]::Parse([FlaUI.Core.WindowsAPI.VirtualKeyShort], $nameOf, $true) } catch { Fail 2 "unknown key '$t' in '$chord'." }
    }
}

try {
    $ids = Get-ProcessIds
    $tops = Get-TopWindows
    $scopes = Get-Scopes $tops

    switch ($Action) {
        'windows' {
            foreach ($w in Get-AllWindows $tops) { Format-Element $w }
        }
        'tree' {
            $starts = if ($Id -or $Name -or $ControlType) { @(Resolve-One $scopes) } else { $scopes }
            foreach ($s in $starts) { Write-Tree $s 0 }
        }
        'find' {
            $hits = Wait-Until { $all = Find-Matches $scopes; if ($all.Count) { , $all } }
            if (-not $hits) { Fail 1 "not found: $(Describe-Query)." }
            foreach ($h in $hits) { Format-Element $h }
        }
        'invoke' {
            $e = Resolve-One $scopes
            $via = Press $e
            "invoked via=$via $(Format-Element $e)"
        }
        'toggle' {
            $e = Resolve-One $scopes
            Need-Pattern $e 'Toggle'
            $p = $e.Patterns.Toggle.Pattern
            if ($Value) {
                $want = [Enum]::Parse([FlaUI.Core.Definitions.ToggleState], $Value, $true)
                $tries = 0
                while ($p.ToggleState.Value -ne $want -and $tries++ -lt 3) { $p.Toggle() }
                if ($p.ToggleState.Value -ne $want) { Fail 1 "toggle stayed $($p.ToggleState.Value), wanted $want." }
            }
            else { $p.Toggle() }
            "toggled $(Format-Element $e)"
        }
        'select' {
            $e = Resolve-One $scopes
            if (-not $Item) {
                Need-Pattern $e 'SelectionItem'
                $e.Patterns.SelectionItem.Pattern.Select()
                "selected $(Format-Element $e)"
                break
            }
            $opened = $false
            if ((Get-PatternNames $e) -contains 'ExpandCollapse' -and $e.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value -eq 'Collapsed') {
                $e.Patterns.ExpandCollapse.Pattern.Expand(); $opened = $true
            }
            $target = Wait-Until { Get-Items $e | Where-Object { $_.Properties.Name.ValueOrDefault -like $Item } | Select-Object -First 1 }
            if (-not $target) {
                if ($opened) { Safe { $e.Patterns.ExpandCollapse.Pattern.Collapse() } | Out-Null }
                Fail 1 "item '$Item' not found in $(Describe-Query)."
            }
            Need-Pattern $target 'SelectionItem'
            $target.Patterns.SelectionItem.Pattern.Select()
            if ($opened -and $e.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value -ne 'Collapsed') {
                Safe { $e.Patterns.ExpandCollapse.Pattern.Collapse() } | Out-Null
            }
            "selected item '$($target.Name)' in $(Format-Element $e)"
        }
        { $_ -in 'expand', 'collapse' } {
            $e = Resolve-One $scopes
            Need-Pattern $e 'ExpandCollapse'
            $p = $e.Patterns.ExpandCollapse.Pattern
            if ($Action -eq 'expand') { $p.Expand() } else { $p.Collapse() }
            $want = if ($Action -eq 'expand') { 'Expanded' } else { 'Collapsed' }
            Wait-Until { $p.ExpandCollapseState.Value -eq $want } 2 | Out-Null
            "$Action $(Format-Element $e)"
        }
        'setvalue' {
            $e = Resolve-One $scopes
            $patterns = Get-PatternNames $e
            if ($patterns -contains 'Value') {
                $e.Patterns.Value.Pattern.SetValue($Value)
                Wait-Until { $e.Patterns.Value.Pattern.Value.Value -eq $Value } 2 | Out-Null
            }
            elseif ($patterns -contains 'RangeValue') { $e.Patterns.RangeValue.Pattern.SetValue([double]$Value) }
            else { Need-Pattern $e 'Value' }
            "set $(Format-Element $e)"
        }
        'items' {
            $e = Resolve-One $scopes
            $opened = $false
            if (-not $Realize -and (Get-PatternNames $e) -contains 'ExpandCollapse' -and $e.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value -eq 'Collapsed') {
                $e.Patterns.ExpandCollapse.Pattern.Expand(); $opened = $true
                Wait-Until { (Get-Items $e).Count } | Out-Null
            }
            $list = if ($Realize) { @(Get-RealizedItems $e) } else { Get-Items $e }
            foreach ($i in $list) { Format-Element $i }
            "count=$($list.Count)"
            if ($opened) { Safe { $e.Patterns.ExpandCollapse.Pattern.Collapse() } | Out-Null }
        }
        'wait' {
            if (-not ($Id -or $Name -or $ControlType)) { Fail 2 'wait needs -Id, -Name, or -ControlType.' }
            if ($Gone) {
                $ok = Wait-Until { (Find-Matches (Find-TopWindows $script:ids)).Count -eq 0 }
                if (-not $ok) { Fail 1 "timeout after ${Timeout}s: $(Describe-Query) is still there." }
                "gone $(Describe-Query)"
            }
            else {
                $hits = Wait-Until { $all = Find-Matches (Find-TopWindows $script:ids); if ($all.Count) { , $all } }
                if (-not $hits) { Fail 1 "timeout after ${Timeout}s: $(Describe-Query) did not appear." }
                Format-Element $hits[0]
            }
        }
        'dialog' {
            $dialogs = Wait-Until { $d = Get-Dialogs $tops; if ($d.Count) { , $d } }
            if (-not $dialogs) { Fail 1 "no dialog titled '$Title'." }
            $dialog = $dialogs[0]
            if (-not $Button) {
                foreach ($d in $dialogs) {
                    "dialog $(Format-Element $d)"
                    foreach ($t in @(Safe { $d.FindAllDescendants($cf.ByControlType($CT::Text)) })) { "  text '$(Clip $t.Name 400)'" }
                    foreach ($b in @(Safe { $d.FindAllDescendants($cf.ByControlType($CT::Button)) })) { "  $(Format-Element $b)" }
                }
                break
            }
            $norm = { param($s) (($s -replace "[‘’]", "'") -replace '&', '') }
            $want = & $norm $Button
            $target = @(Safe { $dialog.FindAllDescendants($cf.ByControlType($CT::Button)) }) |
                Where-Object { (& $norm $_.Properties.Name.ValueOrDefault) -like $want } | Select-Object -First 1
            if (-not $target) { Fail 1 "button '$Button' not found in dialog '$($dialog.Name)'." }
            if ((Test-Destructive $target.Name) -and -not $Yes) {
                Fail 4 "'$($target.Name)' is destructive; confirm with the user, then pass -Yes."
            }
            $title = $dialog.Name
            $label = $target.Name
            $via = Press $target
            $closed = [bool](Wait-Until {
                    $now = @(Find-TopWindows $ids)
                    -not $now.Count -or -not @(Get-Dialogs $now | Where-Object { $_.Properties.Name.ValueOrDefault -eq $title }).Count
                } 3)
            "pressed '$label' via=$via in dialog '$title' closed=$closed"
        }
        'keys' {
            if (-not ($Value -or $Keys)) { Fail 2 'keys needs -Value (text to type) or -Keys (a chord such as Ctrl+S).' }
            $e = if ($Id -or $Name -or $ControlType) { Resolve-One $scopes } else { $tops[0] }
            Safe { $tops[0].AsWindow().SetForeground() } | Out-Null
            Safe { $e.Focus() } | Out-Null
            if ($Value) { Send-Text $Value }
            if ($Keys) { [FlaUI.Core.Input.Keyboard]::TypeSimultaneously([FlaUI.Core.WindowsAPI.VirtualKeyShort[]]@(ConvertTo-Keys $Keys)) }
            Start-Sleep -Milliseconds 50
            $focused = Safe { $automation.FocusedElement() }
            "sent$(if ($Value) { " text='$(Clip $Value)'" })$(if ($Keys) { " keys=$Keys" }) focused=$(if ($focused) { Format-Element $focused })"
        }
        'shot' {
            if (-not $Path) { Fail 2 'shot needs -Path <file.png>.' }
            $e = if ($Id -or $Name -or $ControlType) { Resolve-One $scopes } else { $scopes[0] }
            $full = [IO.Path]::GetFullPath($Path)
            $image = [FlaUI.Core.Capturing.Capture]::Element($e)
            try { $image.ToFile($full); "saved $full $($image.Bitmap.Width)x$($image.Bitmap.Height) $(Format-Element $e)" }
            finally { $image.Dispose() }
        }
    }
}
finally {
    $automation.Dispose()
}
