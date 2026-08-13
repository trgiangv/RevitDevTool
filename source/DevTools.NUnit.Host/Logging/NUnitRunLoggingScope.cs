using System.Diagnostics;
using DevTools.Logging.Listeners;
using DevTools.NUnit.Core.Contracts;
using Microsoft.Extensions.Logging;
using MsLogger = Microsoft.Extensions.Logging.ILogger;

namespace DevTools.NUnit.Host.Logging;

/// <summary>
/// Routes Trace/Debug (and optional Console) into <see cref="ILogger"/> for the host log.
/// Per-case <see cref="NUnitCaseResult.Output"/> is captured in Runtime, not here.
/// </summary>
public sealed class NUnitRunLoggingScope : IDisposable
{
    private readonly NUnitLoggingTraceListener _traceListener;
    private readonly ConsoleRedirector? _consoleRedirector;
    private bool _disposed;

    public NUnitRunLoggingScope(MsLogger logger, bool redirectConsole = true)
    {
        if (logger is null)
            throw new ArgumentNullException(nameof(logger));

        Tracker = new NUnitRunOutputTracker();
        _traceListener = new NUnitLoggingTraceListener(logger, Tracker);
        Trace.Listeners.Insert(0, _traceListener);

        if (redirectConsole)
            _consoleRedirector = new ConsoleRedirector();
    }

    internal NUnitRunOutputTracker Tracker { get; }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _consoleRedirector?.Dispose();
        Trace.Listeners.Remove(_traceListener);
        _traceListener.Dispose();
    }
}
