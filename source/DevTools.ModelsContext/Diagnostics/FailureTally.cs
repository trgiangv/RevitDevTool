using Microsoft.Extensions.Logging;

namespace DevTools.ModelsContext.Diagnostics;

public sealed class FailureTally
{
    private readonly ILogger _logger;

    private FailureTally(ILogger logger)
    {
        _logger = logger;
    }

    public int Failed { get; private set; }

    public Exception? First { get; private set; }

    public static FailureTally? Start(ILogger logger)
    {
        return logger.IsEnabled(LogLevel.Trace) ? new FailureTally(logger) : null;
    }

    public void Add(Exception exception)
    {
        Failed++;
        First ??= exception;
    }

    public void Flush<T>(string operation, T subject, int total)
    {
        if (First is not { } first)
        {
            return;
        }

        _logger.SliceFailed(first, operation, subject?.ToString() ?? string.Empty, Failed, total);
        Failed = 0;
        First = null;
    }
}
