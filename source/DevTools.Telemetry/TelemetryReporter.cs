using System.Diagnostics;

namespace DevTools.Telemetry;

/// <summary>
/// Decides which exceptions are DevTools (internal) failures and so go to Sentry.
/// External code (scripts, commands, toolsets) is marked where DevTools calls it.
/// </summary>
public static class TelemetryReporter
{
    private const string ExternalKey = "devtools.telemetry.external";

    private static readonly string[] InternalAssemblyPrefixes = ["DevTools.", "RevitDevTool", "AcadDevTool"];

    public static TException MarkExternal<TException>(TException exception) where TException : Exception
    {
        exception.Data[ExternalKey] = true;
        return exception;
    }

    public static bool IsExternal(Exception exception) =>
        Chain(exception).Any(e => e.Data.Contains(ExternalKey));

    public static bool IsInternal(Exception exception) =>
        Chain(exception).Any(e => new StackTrace(e, false).GetFrames()?.Any(IsInternalFrame) == true);

    public static bool IsCancellation(Exception exception) =>
        Chain(exception).Any(e => e is OperationCanceledException or TimeoutException);

    public static bool ShouldReport(Exception exception) =>
        !IsCancellation(exception) && !IsExternal(exception) && IsInternal(exception);

    private static bool IsInternalFrame(StackFrame? frame)
    {
        var name = frame?.GetMethod()?.DeclaringType?.Assembly.GetName().Name;
        return name is not null && InternalAssemblyPrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (var e = exception; e is not null; e = e.InnerException)
        {
            yield return e;
        }
    }
}
