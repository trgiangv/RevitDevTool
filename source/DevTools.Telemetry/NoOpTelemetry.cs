namespace DevTools.Telemetry;

/// <summary>
/// No network I/O. Used when user disabled telemetry.
/// </summary>
public sealed class NoOpTelemetry : ITelemetry
{
    public ITelemetryOperation StartOperation(string operation, string kind) => NoOpOperation.Instance;

    public void ReportError(Exception exception, string operation, string kind)
    {
    }

    public void Dispose()
    {
    }

    private sealed class NoOpOperation : ITelemetryOperation
    {
        public static readonly NoOpOperation Instance = new();

        public void Complete()
        {
        }

        public void Cancel()
        {
        }

        public void Fail(Exception? exception = null)
        {
        }

        public void Dispose()
        {
        }
    }
}
