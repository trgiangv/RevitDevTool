using System.Text.RegularExpressions;

namespace DevTools.Telemetry;

/// <summary>
/// Redacts Windows-style absolute paths from free text before export.
/// </summary>
// ReSharper disable once PartialTypeWithSinglePart
public static partial class TelemetryPathScrubber
{
    private const string WindowsPathPattern = """(?<![\w/])[A-Za-z]:\\(?:[^\\/:*?"<>|\r\n]+\\)*[^\\/:*?"<>|\r\n]+""";
#if NET7_0_OR_GREATER
    [GeneratedRegex(WindowsPathPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex();
    private static readonly Regex WindowsPathRegex = MyRegex();
#else
    private static readonly Regex WindowsPathRegex = new(WindowsPathPattern,RegexOptions.Compiled | RegexOptions.CultureInvariant);
#endif

    public static string Scrub(string? text)
    {
        return string.IsNullOrEmpty(text) ? string.Empty : WindowsPathRegex.Replace(text, "[path]");
    }


}
