namespace DevTools.Telemetry;

/// <summary>
/// Decides whether an exception should be sent as critical telemetry.
/// </summary>
public static class TelemetryReporting
{
    public static bool ShouldReportCriticalException(Exception exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            switch (e)
            {
                case OperationCanceledException or TaskCanceledException:
                case TimeoutException:
                    return false;
            }
        }

        return true;
    }
}
