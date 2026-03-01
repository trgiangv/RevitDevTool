using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using System.IO;

namespace RevitDevTool.Scintilla.Benchmarks.Benchmarking;

/// <summary>
/// Config for benchmarks that require WinForms controls (STA thread, no isolated process).
/// Uses InProcessEmitToolchain to avoid BDN's isolated process which lacks a Windows message loop.
/// </summary>
internal sealed class InProcessBenchmarkConfig : ManualConfig
{
    public InProcessBenchmarkConfig()
    {
        var artifactPath = Environment.GetEnvironmentVariable("RDT_BENCH_ARTIFACTS");
        if (string.IsNullOrWhiteSpace(artifactPath))
            artifactPath = Path.Combine(Environment.CurrentDirectory, "BenchmarkDotNet.Artifacts");
        WithArtifactsPath(artifactPath);

        AddJob(
            Job.Default
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithWarmupCount(3)
                .WithIterationCount(8)
                .WithLaunchCount(1));

        AddDiagnoser(MemoryDiagnoser.Default);
        WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));
    }
}
