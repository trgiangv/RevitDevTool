using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace ZLogger.RichTextBox.Winforms.Benchmark;

/// <summary>
/// Custom BenchmarkDotNet configuration for Windows Forms projects.
/// Uses InProcess toolchain to avoid net8.0-windows compatibility issues.
/// </summary>
public class WindowsFormsConfig : ManualConfig
{
    public WindowsFormsConfig()
    {
        // Use InProcess toolchain - runs benchmarks in the same process
        // This avoids BenchmarkDotNet generating incompatible projects for net8.0-windows
        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core80)
            .WithToolchain(InProcessEmitToolchain.Instance)
            .WithId("InProcess"));

        // Add default exporters and analyzers
        AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default);
        AddColumnProvider(BenchmarkDotNet.Columns.DefaultColumnProviders.Instance);
        AddExporter(BenchmarkDotNet.Exporters.MarkdownExporter.GitHub);
        AddExporter(BenchmarkDotNet.Exporters.HtmlExporter.Default);
    }
}

/// <summary>
/// Short run config for quick testing (not for final results)
/// </summary>
public class QuickConfig : ManualConfig
{
    public QuickConfig()
    {
        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core80)
            .WithToolchain(InProcessEmitToolchain.Instance)
            .WithWarmupCount(1)
            .WithIterationCount(3)
            .WithId("Quick"));

        AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default);
        AddColumnProvider(BenchmarkDotNet.Columns.DefaultColumnProviders.Instance);
    }
}
