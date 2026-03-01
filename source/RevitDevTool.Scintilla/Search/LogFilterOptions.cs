using Microsoft.Extensions.Logging;

namespace RevitDevTool.Scintilla.Search;

public sealed class LogFilterOptions
{
    public static readonly LogFilterOptions All = new();

    public HashSet<LogLevel> AllowedLevels { get; init; } = new();
    public string? TextContains { get; init; }
    public bool MatchCase { get; init; }
    public bool IsAll => AllowedLevels.Count == 0 && string.IsNullOrEmpty(TextContains);

}
