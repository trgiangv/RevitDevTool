namespace RevitDevTool.Scintilla.Search;

public readonly record struct LogSearchResult(bool Found, int StartPosition, int Length)
{
    public static LogSearchResult NotFound => new(false, -1, 0);
}
