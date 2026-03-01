namespace RevitDevTool.Scintilla.Search;

public sealed class LogSearchRequest
{
    public string Pattern { get; init; } = string.Empty;
    public bool MatchCase { get; init; }
    public bool UseRegex { get; init; }
    public bool SearchBackward { get; init; }
    public bool HighlightOnly { get; init; }
}
