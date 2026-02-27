using Microsoft.Extensions.Logging;

namespace RevitDevTool.Scintilla.Logging;

public sealed class ScintillaLoggerOptions
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
    public bool IncludeScopes { get; set; } = true;
    public Func<string, bool>? CategoryFilter { get; set; }
}
