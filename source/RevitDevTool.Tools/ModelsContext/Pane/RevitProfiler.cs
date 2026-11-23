using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RevitDevTool.Core;
using RevitDevTool.Tools.ModelsContext.Documents;
using System.Diagnostics;
using ZLogger;

namespace RevitDevTool.Tools.ModelsContext.Pane;

internal static class RevitProfiler
{
    public const double SlowRunMs = 250;

    public static ILogger Logger { get; private set; } = NullLogger.Instance;

    public static void Use(ILogger logger)
    {
        Logger = logger;
    }

    public static Task<T> RunAsync<T>(
        string operation,
        Func<UIApplication, T> work,
        CancellationToken cancellationToken,
        ProfileStats? stats = null)
    {
        var logger = Logger;
        if (!logger.IsEnabled(LogLevel.Trace))
        {
            return RevitContextExecutor.RaiseAsync(work, cancellationToken);
        }

        var queued = Stopwatch.GetTimestamp();
        return RevitContextExecutor.RaiseAsync(application =>
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                return work(application);
            }
            finally
            {
                Report(logger, operation, stats, queued, started);
            }
        }, cancellationToken);
    }

    public static Task RunAsync(string operation, Action<UIApplication> work, CancellationToken cancellationToken)
    {
        var logger = Logger;
        if (!logger.IsEnabled(LogLevel.Trace))
        {
            return RevitContextExecutor.RaiseAsync(work, cancellationToken);
        }

        var queued = Stopwatch.GetTimestamp();
        return RevitContextExecutor.RaiseAsync(application =>
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                work(application);
            }
            finally
            {
                Report(logger, operation, null, queued, started);
            }
        }, cancellationToken);
    }

    public static string LabelOf<T>(T subject)
    {
        return subject switch
        {
            SourceId source => source.Label,
            DocumentKey document => DocumentRegistry.LabelOf(document),
            _ => subject?.ToString() ?? string.Empty
        };
    }

    private static void Report(ILogger logger, string operation, ProfileStats? stats, long queued, long started)
    {
        var queueMs = TraceTimer.MillisecondsBetween(queued, started);
        var runMs = TraceTimer.MillisecondsSince(started);
        if (stats is null || runMs >= SlowRunMs)
        {
            logger.ZLogTrace($"Revit {operation}: queue {queueMs} ms, run {runMs} ms");
        }

        stats?.Add(queueMs, runMs);
    }
}

internal sealed class ProfileStats
{
    private readonly ILogger _logger;
    private readonly string _operation;
    private readonly long _start = Stopwatch.GetTimestamp();
    private int _calls;
    private double _maxRunMs;
    private double _queueMs;
    private double _runMs;

    private ProfileStats(ILogger logger, string operation)
    {
        _logger = logger;
        _operation = operation;
    }

    public static ProfileStats? Start(string operation)
    {
        var logger = RevitProfiler.Logger;
        return logger.IsEnabled(LogLevel.Trace) ? new ProfileStats(logger, operation) : null;
    }

    public void Add(double queueMs, double runMs)
    {
        _calls++;
        _queueMs += queueMs;
        _runMs += runMs;
        _maxRunMs = Math.Max(_maxRunMs, runMs);
    }

    public void Flush<T>(T subject, int items)
    {
        var label = RevitProfiler.LabelOf(subject);
        var totalMs = TraceTimer.MillisecondsSince(_start);
        _logger.ZLogTrace(
            $"Revit {_operation} {label}: {items} items in {_calls} slices, queue {Math.Round(_queueMs, 1)} ms, run {Math.Round(_runMs, 1)} ms (max {_maxRunMs} ms), total {totalMs} ms");
    }
}
