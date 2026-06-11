#if DEBUG
using System.Text;
namespace RevitDevTool.ExternalEvent.App.Commands;

internal sealed class BenchmarkReport
{
    private static readonly HashSet<string> CapabilityCategories =
    [
        nameof(BenchmarkCategory.CancellationLifecycle),
        nameof(BenchmarkCategory.ErrorPropagation),
        nameof(BenchmarkCategory.NestedReentry),
    ];

    public List<BenchmarkResult> Results { get; init; } = [];

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Benchmark Summary");
        sb.AppendLine();

        foreach (var suiteGroup in Results.GroupBy(r => r.Suite))
        {
            var suiteLabel = suiteGroup.Key == BenchmarkSuite.CentralDispatcher
                ? "SUITE 1: CENTRAL DISPATCHER (arbitrary delegate execution)"
                : "SUITE 2: IN-CONTEXT EVENT REUSE (dispatch overhead only)";

            sb.AppendLine($"## {suiteLabel}");
            sb.AppendLine();

            foreach (var catGroup in suiteGroup.GroupBy(r => r.Category))
            {
                var catName = catGroup.Key;
                sb.AppendLine($"### {catName}");
                sb.AppendLine();

                if (CapabilityCategories.Contains(catName))
                    AppendCapabilityTable(sb, catGroup);
                else
                    AppendThroughputTable(sb, catGroup);
            }
        }

        return sb.ToString();
    }

    private static void AppendThroughputTable(StringBuilder sb, IGrouping<string, BenchmarkResult> catGroup)
    {
        sb.AppendLine("| Adapter | Requested | Completed | Faulted | Cancelled | TimedOut | Wall ms | req/s |");
        sb.AppendLine("|---------|-----------|-----------|---------|-----------|---------|---------|-------|");
        foreach (var r in catGroup)
        {
            sb.AppendLine($"| {r.AdapterName} | {r.TotalRequested} | {r.Completed} | " +
                          $"{r.Faulted} | {r.Cancelled} | {r.TimedOut} | " +
                          $"{r.WallTimeMs:F0} | {r.ThroughputRps:F0} |");
        }
        sb.AppendLine();

        var withLatency = catGroup.Where(r => r.TotalLatency != null).ToArray();
        if (withLatency.Length > 0)
        {
            sb.AppendLine("| Adapter | mean | p50 | p95 | p99 | max |");
            sb.AppendLine("|---------|------|-----|-----|-----|-----|");
            foreach (var r in withLatency)
            {
                var s = r.TotalLatency!;
                sb.AppendLine($"| {r.AdapterName} | {s.Mean:F1}μs | {s.Median:F1}μs | {s.P95:F1}μs | {s.P99:F1}μs | {s.Max:F1}μs |");
            }
            sb.AppendLine();
        }
    }

    private static void AppendCapabilityTable(StringBuilder sb, IGrouping<string, BenchmarkResult> catGroup)
    {
        sb.AppendLine("| Adapter | Requested | Completed | Result | Notes |");
        sb.AppendLine("|---------|-----------|-----------|--------|-------|");
        foreach (var r in catGroup)
        {
            var verdict = r.Notes != null && r.Notes.Contains("not supported", StringComparison.OrdinalIgnoreCase)
                ? "SKIPPED"
                : r.Completed >= r.TotalRequested ? "PASS" : "PARTIAL";

            sb.AppendLine($"| {r.AdapterName} | {r.TotalRequested} | {r.Completed} | " +
                          $"{verdict} | {r.Notes ?? "—"} |");
        }
        sb.AppendLine();
    }
}
#endif
