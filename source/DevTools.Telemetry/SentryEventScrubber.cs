using Sentry.Protocol;

namespace DevTools.Telemetry;

/// <summary>
/// Last-chance privacy pass applied in <c>BeforeSend</c>: drops the machine name and redacts
/// absolute paths from messages, exception values, mechanism data, and stack frames.
/// </summary>
public static class SentryEventScrubber
{
    public static SentryEvent Scrub(SentryEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        evt.ServerName = null;
        if (evt.Message is { } message)
        {
            message.Message = ScrubOrNull(message.Message);
            message.Formatted = ScrubOrNull(message.Formatted);
        }

        foreach (var exception in evt.SentryExceptions ?? [])
        {
            Scrub(exception);
        }

        return evt;
    }

    private static void Scrub(SentryException exception)
    {
        exception.Value = ScrubOrNull(exception.Value);

        if (exception.Mechanism is { } mechanism)
        {
            foreach (var key in mechanism.Data.Keys.ToList())
            {
                if (mechanism.Data[key] is string text)
                {
                    mechanism.Data[key] = TelemetryPathScrubber.Scrub(text);
                }
            }
        }

        foreach (var frame in exception.Stacktrace?.Frames ?? [])
        {
            frame.AbsolutePath = null;
            if (frame.FileName is { Length: > 0 } fileName)
            {
                frame.FileName = Path.GetFileName(fileName);
            }
        }
    }

    private static string? ScrubOrNull(string? text) => text is null ? null : TelemetryPathScrubber.Scrub(text);
}
