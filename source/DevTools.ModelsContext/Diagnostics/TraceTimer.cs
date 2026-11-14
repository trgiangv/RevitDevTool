using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DevTools.ModelsContext.Diagnostics;

public readonly struct TraceTimer : IDisposable
{
    private readonly ILogger? _logger;
    private readonly string? _operation;
    private readonly long _start;

    private TraceTimer(ILogger logger, string? operation)
    {
        _logger = logger;
        _operation = operation;
        _start = Stopwatch.GetTimestamp();
    }

    public bool IsEnabled => _logger is not null;

    public double ElapsedMs => _logger is null ? 0 : MillisecondsSince(_start);

    public static TraceTimer Start(ILogger logger, string? operation = null)
    {
        return logger.IsEnabled(LogLevel.Trace) ? new TraceTimer(logger, operation) : default;
    }

    public static double MillisecondsSince(long start)
    {
        return MillisecondsBetween(start, Stopwatch.GetTimestamp());
    }

    public static double MillisecondsBetween(long start, long end)
    {
        return Math.Round((end - start) * 1000d / Stopwatch.Frequency, 1);
    }

    public void Dispose()
    {
        if (_logger is not null && _operation is not null)
        {
            _logger.Timed(_operation, ElapsedMs);
        }
    }
}
