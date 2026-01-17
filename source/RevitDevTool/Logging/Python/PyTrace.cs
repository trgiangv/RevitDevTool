using RevitDevTool.Settings;
using System.Diagnostics;

namespace RevitDevTool.Logging.Python;

/// <summary>
/// Static bridge for Python (IronPython) scripts to log via standard .NET Trace.
/// This respects the application's IncludeStackTrace setting.
/// </summary>
[PublicAPI]
public static class PyTrace
{
    private static ISettingsService _settingsService = null!;

    /// <summary>
    /// Initializes the logger with settings service.
    /// </summary>
    public static void Initialize(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public static void Write(string message, string traceback)
    {
        var config = _settingsService.LogConfig;
        if (config.IncludeStackTrace && !string.IsNullOrWhiteSpace(traceback))
        {
            var lines = traceback.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            var filteredLines = lines
                .Where(line => line.TrimStart().StartsWith("File", StringComparison.OrdinalIgnoreCase))
                .Take(config.StackTraceDepth);

            message = $"{message}\nTraceback (Last call first):\n{string.Join(Environment.NewLine, filteredLines)}";
        }

        Trace.Write(message);
    }
}
