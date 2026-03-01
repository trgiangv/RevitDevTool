param(
    [string]$ResultsDirectory = "",
    [double]$TolerancePercent = 5.0
)

if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $envResults = $env:RDT_BENCH_RESULTS
    if (-not [string]::IsNullOrWhiteSpace($envResults)) {
        $ResultsDirectory = $envResults
    } else {
        $ResultsDirectory = Join-Path (Get-Location) "BenchmarkDotNet.Artifacts\results"
    }
}

function Convert-BdnMeanToMilliseconds([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return $null }
    $parts = $value.Trim() -split "\s+"
    if ($parts.Length -lt 2) { return $null }
    $num = [double]$parts[0]
    $unit = $parts[1].ToLowerInvariant()
    switch ($unit) {
        "ns" { return $num / 1000000.0 }
        "us" { return $num / 1000.0 }
        "ms" { return $num }
        "s"  { return $num * 1000.0 }
        default { return $null }
    }
}

function Assert-FileExists([string]$path) {
    if (-not (Test-Path $path)) {
        throw "Required benchmark result file not found: $path"
    }
}

$appendCsv = Join-Path $ResultsDirectory "RevitDevTool.Scintilla.Benchmarks.Benchmarks.ComboAppendBenchmarks-report.csv"
$colorizedCsv = Join-Path $ResultsDirectory "RevitDevTool.Scintilla.Benchmarks.Benchmarks.ComboColorizedBenchmarks-report.csv"
$searchCsv = Join-Path $ResultsDirectory "RevitDevTool.Scintilla.Benchmarks.Benchmarks.ComboSearchFilterBenchmarks-report.csv"

Assert-FileExists $appendCsv
Assert-FileExists $colorizedCsv
Assert-FileExists $searchCsv

$appendRows = Import-Csv $appendCsv
$colorizedRows = Import-Csv $colorizedCsv
$searchRows = Import-Csv $searchCsv

$failures = New-Object System.Collections.Generic.List[string]

function Is-SlowerThanWithTolerance([double]$leftMs, [double]$rightMs, [double]$tolerancePercent) {
    $threshold = $rightMs * (1.0 + ($tolerancePercent / 100.0))
    return $leftMs -gt $threshold
}

# Guard 1: Append combo - ZLogger+Scintilla should not be slower than Serilog+RichTextBox
$appendSerilog = $appendRows | Where-Object { $_.Method -like "*Serilog + RichTextBox append*" } | Select-Object -First 1
$appendZlogger = $appendRows | Where-Object { $_.Method -like "*ZLogger + Scintilla append*" } | Select-Object -First 1
if ($null -ne $appendSerilog -and $null -ne $appendZlogger) {
    $serilogMs = Convert-BdnMeanToMilliseconds $appendSerilog.Mean
    $zloggerMs = Convert-BdnMeanToMilliseconds $appendZlogger.Mean
    if ($null -ne $serilogMs -and $null -ne $zloggerMs -and (Is-SlowerThanWithTolerance $zloggerMs $serilogMs $TolerancePercent)) {
        $failures.Add("Append regression: ZLogger+Scintilla=$($appendZlogger.Mean) slower than Serilog+RichTextBox=$($appendSerilog.Mean) beyond tolerance $TolerancePercent%.")
    }
}

# Guard 2: Colorized combo - ZLogger+Scintilla should not be slower than Serilog+RichTextBox
$colorSerilog = $colorizedRows | Where-Object { $_.Method -like "*Serilog + RichTextBox colorized*" } | Select-Object -First 1
$colorZlogger = $colorizedRows | Where-Object { $_.Method -like "*ZLogger + Scintilla colorized*" } | Select-Object -First 1
if ($null -ne $colorSerilog -and $null -ne $colorZlogger) {
    $serilogMs = Convert-BdnMeanToMilliseconds $colorSerilog.Mean
    $zloggerMs = Convert-BdnMeanToMilliseconds $colorZlogger.Mean
    if ($null -ne $serilogMs -and $null -ne $zloggerMs -and (Is-SlowerThanWithTolerance $zloggerMs $serilogMs $TolerancePercent)) {
        $failures.Add("Colorized regression: ZLogger+Scintilla=$($colorZlogger.Mean) slower than Serilog+RichTextBox=$($colorSerilog.Mean) beyond tolerance $TolerancePercent%.")
    }
}

# Guard 3: Search/Filter combo - Scintilla should not be slower than RichTextBox equivalent operation
$rtbFilter = $searchRows | Where-Object { $_.Method -like "*RichTextBox filter contains*" } | Select-Object -First 1
$sciFilter = $searchRows | Where-Object { $_.Method -like "*Scintilla filter contains*" } | Select-Object -First 1
if ($null -ne $rtbFilter -and $null -ne $sciFilter) {
    $rtbMs = Convert-BdnMeanToMilliseconds $rtbFilter.Mean
    $sciMs = Convert-BdnMeanToMilliseconds $sciFilter.Mean
    if ($null -ne $rtbMs -and $null -ne $sciMs -and (Is-SlowerThanWithTolerance $sciMs $rtbMs $TolerancePercent)) {
        $failures.Add("Filter regression: Scintilla=$($sciFilter.Mean) slower than RichTextBox=$($rtbFilter.Mean) beyond tolerance $TolerancePercent%.")
    }
}

$rtbSearch = $searchRows | Where-Object { $_.Method -like "*RichTextBox search next*" } | Select-Object -First 1
$sciSearch = $searchRows | Where-Object { $_.Method -like "*Scintilla search next*" } | Select-Object -First 1
if ($null -ne $rtbSearch -and $null -ne $sciSearch) {
    $rtbMs = Convert-BdnMeanToMilliseconds $rtbSearch.Mean
    $sciMs = Convert-BdnMeanToMilliseconds $sciSearch.Mean
    if ($null -ne $rtbMs -and $null -ne $sciMs -and (Is-SlowerThanWithTolerance $sciMs $rtbMs $TolerancePercent)) {
        $failures.Add("Search regression: Scintilla=$($sciSearch.Mean) slower than RichTextBox=$($rtbSearch.Mean) beyond tolerance $TolerancePercent%.")
    }
}

if ($failures.Count -gt 0) {
    Write-Host "Benchmark regression guards failed:" -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Benchmark regression guards passed." -ForegroundColor Green
