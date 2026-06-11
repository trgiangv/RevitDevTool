#if DEBUG
namespace RevitDevTool.ExternalEvent.App.Commands;

internal sealed class PercentileStats
{
    public double Mean { get; init; }
    public double Median { get; init; }
    public double P95 { get; init; }
    public double P99 { get; init; }
    public double Max { get; init; }

    public static PercentileStats FromSorted(double[] sorted)
    {
        if (sorted.Length == 0)
            return new PercentileStats();

        return new PercentileStats
        {
            Mean = sorted.Average(),
            Median = Percentile(sorted, 50),
            P95 = Percentile(sorted, 95),
            P99 = Percentile(sorted, 99),
            Max = sorted[^1],
        };
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

    public override string ToString() =>
        $"mean={Mean:F1}μs  p50={Median:F1}μs  p95={P95:F1}μs  p99={P99:F1}μs  max={Max:F1}μs";
}
#endif
