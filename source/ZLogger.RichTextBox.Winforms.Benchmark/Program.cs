using BenchmarkDotNet.Running;
using ZLogger.RichTextBox.Winforms.Benchmark;

// Interactive mode - visual stress test
if (args.Length > 0 && args[0] == "--interactive")
{
    InteractiveTestRunner.Run();
    return;
}

// BenchmarkDotNet mode
// Run with: dotnet run -c Release
// Or specific benchmark: dotnet run -c Release -- --filter *ThroughputBenchmark*
// Interactive visual test: dotnet run -- --interactive

Console.WriteLine("=== ZLogger vs Serilog RichTextBox Sink Benchmark ===");
Console.WriteLine();
Console.WriteLine("Available benchmarks:");
Console.WriteLine("  1. ThroughputBenchmark - Raw logging throughput");
Console.WriteLine("  2. MemoryBenchmark - Memory allocation patterns");
Console.WriteLine("  3. LatencyBenchmark - End-to-end latency");
Console.WriteLine("  4. StressBenchmark - High-volume stress test");
Console.WriteLine("  5. RenderingBenchmark - RTF rendering performance");
Console.WriteLine();
Console.WriteLine("For interactive visual test, run: dotnet run -- --interactive");
Console.WriteLine();

// Use custom config for Windows Forms compatibility
var config = new WindowsFormsConfig();
var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);

