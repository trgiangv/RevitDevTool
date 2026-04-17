using System.Diagnostics;
namespace RevitDevTool.ExternalEvent.App.Commands;

internal struct RequestTiming
{
    public long T0;
    public long T1;
    public long T2;
    public long T3;
    public long T4;

    public double EnqueueUs => TicksToUs(T1 - T0);
    public double EventLoopWaitUs => TicksToUs(T2 - T1);
    public double CompletionUs => TicksToUs(T4 - T3);
    public double LibraryTotalUs => EnqueueUs + CompletionUs;
    public double EndToEndUs => TicksToUs(T4 - T0);

    private static double TicksToUs(long ticks)
    {
        return ticks * 1_000_000.0 / Stopwatch.Frequency;
    }
}

internal static class TimingStats
{
    public static string Summarize(string label, IReadOnlyList<RequestTiming> timings)
    {
        if (timings.Count == 0) return $"{label}: no data";

        var enqueue = timings.Select(t => t.EnqueueUs).OrderBy(x => x).ToArray();
        var completion = timings.Select(t => t.CompletionUs).OrderBy(x => x).ToArray();
        var libTotal = timings.Select(t => t.LibraryTotalUs).OrderBy(x => x).ToArray();
        var e2E = timings.Select(t => t.EndToEndUs).OrderBy(x => x).ToArray();
        var eventLoop = timings.Select(t => t.EventLoopWaitUs).OrderBy(x => x).ToArray();

        return $"""
            {label} ({timings.Count} requests)
              Enqueue     : {Fmt(enqueue)}
              Completion  : {Fmt(completion)}
              Library Tot : {Fmt(libTotal)}
              Event Loop  : {Fmt(eventLoop)}
              End-to-End  : {Fmt(e2E)}
            """;
    }

    private static string Fmt(double[] sorted)
    {
        var mean = sorted.Average();
        var median = Percentile(sorted, 50);
        var p95 = Percentile(sorted, 95);
        var p99 = Percentile(sorted, 99);
        return $"mean={mean:F1}us  med={median:F1}us  p95={p95:F1}us  p99={p99:F1}us";
    }

    private static double Percentile(double[] sorted, double p)
    {
        var index = (p / 100.0) * (sorted.Length - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper) return sorted[lower];
        var frac = index - lower;
        return sorted[lower] * (1 - frac) + sorted[upper] * frac;
    }
}
