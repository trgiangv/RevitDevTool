using Microsoft.Extensions.Logging;

namespace DevTools.Telemetry;

/// <summary>
/// Anonymous usage (aggregated per session, sent on <see cref="Flush"/>), breadcrumbs on errors, and critical error reporting (Sentry).
/// Implementations must be safe to call from any thread.
/// </summary>
public interface ITelemetry : IDisposable
{
    void RecordExecutionInvocation(string providerKind, bool succeeded);

    void RecordMcpInvocation(string category);

    void RecordLoggerGeometry(string category);
    
    void RecordLoggerTrace(LogLevel level);

    void RecordCriticalException(
        Exception exception,
        string feature,
        IReadOnlyDictionary<string, string>? tags = null);

    /// <summary>
    /// Sends a session usage summary (when Sentry is enabled), then flushes the transport (call on host shutdown, e.g. Revit close).
    /// </summary>
    void Flush();
}
