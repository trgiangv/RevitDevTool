namespace DevTools.Telemetry;

/// <summary>
/// Resolves the Sentry DSN: <c>SENTRY_DSN</c> environment variable overrides the compiled-in default.
/// </summary>
public static class TelemetryDsnResolver
{
    /// <summary>
    /// Returns a non-empty DSN, or null when telemetry should remain disabled for this process.
    /// </summary>
    /// <param name="builtInDsn">Non-empty DSN compiled into the host add-in (e.g. Revit). Ignored when null or whitespace.</param>
    public static string? TryResolve(string? builtInDsn)
    {
        var fromEnv = Environment.GetEnvironmentVariable("SENTRY_DSN");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        return !string.IsNullOrWhiteSpace(builtInDsn) ? builtInDsn!.Trim() : null;
    }
}
