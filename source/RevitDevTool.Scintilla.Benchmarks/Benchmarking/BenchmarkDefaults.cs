using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;

namespace RevitDevTool.Scintilla.Benchmarks.Benchmarking;

internal sealed class BenchmarkDefaults : ManualConfig
{
    public BenchmarkDefaults()
    {
        WithArtifactsPath(@"C:\bdn-rdt");

        AddJob(
            Job.Default
                .WithWarmupCount(3)
                .WithIterationCount(8)
                .WithLaunchCount(1));

        AddDiagnoser(MemoryDiagnoser.Default);
        WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));
    }
}
