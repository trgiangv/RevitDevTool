using Microsoft.Extensions.Logging;

namespace DevTools.Telemetry;

/// <summary>
/// No network I/O. Used when user disabled telemetry.
/// </summary>
public sealed class NoOpTelemetry : ITelemetry
{
    public void RecordExecutionInvocation(string providerKind, bool succeeded)
    {
    }

    public void RecordMcpInvocation(string category)
    {
    }

    public void RecordLoggerGeometry(string category)
    {
    }

    public void RecordLoggerTrace(LogLevel level)
    {
    }

    public void RecordCriticalException(
        Exception exception,
        string feature,
        IReadOnlyDictionary<string, string>? tags = null)
    {
    }

    public void Flush()
    {
    }

    public void Dispose()
    {
    }
}
