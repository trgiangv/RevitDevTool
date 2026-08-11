using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MsLogger = Microsoft.Extensions.Logging.ILogger;

namespace DevTools.NUnit.Host.Logging;

/// <summary>
/// Routes Trace/Debug output to <see cref="ILogger"/> and the active NUnit test buffer.
/// </summary>
public sealed class NUnitLoggingTraceListener : TraceListener
{
    private readonly MsLogger _logger;
    private readonly NUnitRunOutputTracker _tracker;

    public NUnitLoggingTraceListener(MsLogger logger, NUnitRunOutputTracker tracker)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public override bool IsThreadSafe => true;

    public override void Write(string? message) => Forward(message);

    public override void WriteLine(string? message) => Forward(message, appendNewLine: true);

    private void Forward(string? message, bool appendNewLine = false)
    {
        if (string.IsNullOrEmpty(message))
            return;

        var text = appendNewLine ? message + Environment.NewLine : message;
        _tracker.Append(text);
        _logger.LogInformation("[NUnit] {Message}", message);
    }
}
