param(
    [ValidateSet("full", "core", "pixel", "all")]
    [string]$Suite = "all",
    [string]$Configuration = "Release",
    [switch]$RunRegressionGuard,
    [double]$RegressionTolerancePercent = 5.0
)

$project = "source/RevitDevTool.Scintilla.Benchmarks/RevitDevTool.Scintilla.Benchmarks.csproj"

switch ($Suite) {
    "full" {
        dotnet run --project $project -c $Configuration -- --filter "*ComboAppendBenchmarks*" "*ComboColorizedBenchmarks*" "*ComboSearchFilterBenchmarks*"
    }
    "core" {
        dotnet run --project $project -c $Configuration -- --filter "*CoreBenchmarks*"
    }
    "pixel" {
        dotnet run --project $project -c $Configuration -- --filter "*ComboPixelDrawBenchmarks*"
    }
    default {
        dotnet run --project $project -c $Configuration
    }
}

if ($RunRegressionGuard) {
    .\source\RevitDevTool.Scintilla.Benchmarks\check-regression.ps1 -TolerancePercent $RegressionTolerancePercent
}
