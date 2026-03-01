using BenchmarkDotNet.Running;
using RevitDevTool.Scintilla.Benchmarks.Benchmarks;

// STA thread required for WinForms controls benchmarks.
// InProcessEmitToolchain runs benchmarks on this thread, so STAThread here is sufficient.
internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Application.SetCompatibleTextRenderingDefault(false);
        BenchmarkSwitcher.FromTypes(
        [
            typeof(ComboAppendBenchmarks),
            typeof(ComboAppendCoreBenchmarks),
            typeof(ComboColorizedBenchmarks),
            typeof(ComboColorizedCoreBenchmarks),
            typeof(ComboSearchFilterBenchmarks),
            typeof(ComboSearchFilterCoreBenchmarks),
            typeof(ComboPixelDrawBenchmarks)
        ]).Run(args);
    }
}
