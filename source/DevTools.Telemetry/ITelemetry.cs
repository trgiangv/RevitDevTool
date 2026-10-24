namespace DevTools.Telemetry;

/// <summary>
/// Sentry facade: one operation per execution / MCP call / test run and error reports for DevTools code.
/// Implementations must be safe to call from any thread.
/// </summary>
public interface ITelemetry : IDisposable
{
    ITelemetryOperation StartOperation(string operation, string kind);

    void ReportError(Exception exception, string operation, string kind);
}

public interface ITelemetryOperation : IDisposable
{
    void Complete();

    void Cancel();

    void Fail(Exception? exception = null);
}
