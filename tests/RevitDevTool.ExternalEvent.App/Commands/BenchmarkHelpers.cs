#if DEBUG
namespace RevitDevTool.ExternalEvent.App.Commands;

internal static class BenchmarkHelpers
{
    private const int WarmupCount = 5;
    private const int CooldownMs = 500;

    public static async Task Warmup(IDispatchAdapter adapter)
    {
        for (var i = 0; i < WarmupCount; i++)
        {
            try { await adapter.RunAsync(_ => { }); }
            catch { /* warmup failures are expected for some adapters */ }
        }
    }

    public static async Task Warmup(IInContextEventAdapter adapter)
    {
        for (var i = 0; i < WarmupCount; i++)
        {
            try { await adapter.RaiseAndWaitAsync(); }
            catch { /* warmup failures are expected for some adapters */ }
        }
    }

    public static Task Cooldown() => Task.Delay(CooldownMs);

    public static int[] DistributeWork(int totalCount, int producerCount)
    {
        var baseCount = totalCount / producerCount;
        var remainder = totalCount % producerCount;
        var result = new int[producerCount];
        for (var i = 0; i < producerCount; i++)
            result[i] = baseCount + (i < remainder ? 1 : 0);
        return result;
    }

    public static IReadOnlyList<T> Shuffle<T>(IReadOnlyList<T> items, Random rng)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }

    public static void LogResult(BenchmarkResult result, Action<string> log)
    {
        log($"- **Completed:** {result.Completed}, **Faulted:** {result.Faulted}, " +
            $"**Cancelled:** {result.Cancelled}, **TimedOut:** {result.TimedOut}");
        log($"- **Wall time:** {result.WallTimeMs:F0}ms, **Throughput:** {result.ThroughputRps:F0} req/s");
        if (result.Notes != null)
            log($"- **Note:** {result.Notes}");
        log("");
    }
}
#endif
