using System.Diagnostics;
namespace RevitDevTool.ExternalEvent.App.Commands;

internal struct RequestTiming
{
    /// <summary>Before enqueue / adapter call.</summary>
    public long T0;
    /// <summary>After enqueue returned (task obtained).</summary>
    public long T1;
    /// <summary>Callback started executing on Revit thread.</summary>
    public long T2;
    /// <summary>Callback finished.</summary>
    public long T3;
    /// <summary>Awaited task completed.</summary>
    public long T4;

    public double EnqueueUs => TicksToUs(T1 - T0);
    public double WaitUs => TicksToUs(T2 - T1);
    public double CallbackUs => TicksToUs(T3 - T2);
    public double CompletionUs => TicksToUs(T4 - T3);
    public double TotalUs => TicksToUs(T4 - T0);

    private static double TicksToUs(long ticks)
    {
        return ticks * 1_000_000.0 / Stopwatch.Frequency;
    }
}

#if DEBUG
internal static class TimingStats
{
    public static string Summarize(string label, IReadOnlyList<RequestTiming> timings)
    {
        if (timings.Count == 0) return $"{label}: no data";

        var enqueue = timings.Select(t => t.EnqueueUs).OrderBy(x => x).ToArray();
        var wait = timings.Select(t => t.WaitUs).OrderBy(x => x).ToArray();
        var callback = timings.Select(t => t.CallbackUs).OrderBy(x => x).ToArray();
        var completion = timings.Select(t => t.CompletionUs).OrderBy(x => x).ToArray();
        var total = timings.Select(t => t.TotalUs).OrderBy(x => x).ToArray();

        return $"""
            {label} ({timings.Count} requests)
              Enqueue    : {PercentileStats.FromSorted(enqueue)}
              Wait       : {PercentileStats.FromSorted(wait)}
              Callback   : {PercentileStats.FromSorted(callback)}
              Completion : {PercentileStats.FromSorted(completion)}
              Total      : {PercentileStats.FromSorted(total)}
            """;
    }

    public static (PercentileStats enqueue, PercentileStats wait, PercentileStats execution, PercentileStats total)
        ComputeStats(IReadOnlyList<RequestTiming> timings)
    {
        var enqueue = timings.Select(t => t.EnqueueUs).OrderBy(x => x).ToArray();
        var wait = timings.Select(t => t.WaitUs).OrderBy(x => x).ToArray();
        var callback = timings.Select(t => t.CallbackUs).OrderBy(x => x).ToArray();
        var total = timings.Select(t => t.TotalUs).OrderBy(x => x).ToArray();

        return (
            PercentileStats.FromSorted(enqueue),
            PercentileStats.FromSorted(wait),
            PercentileStats.FromSorted(callback),
            PercentileStats.FromSorted(total)
        );
    }
}
#endif
