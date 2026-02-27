using RevitDevTool.Scintilla.Contracts;

namespace RevitDevTool.Scintilla.Search;

public sealed class LogFilterOptions
{
    public static readonly LogFilterOptions All = new();

    public HashSet<LogSeverity> AllowedLevels { get; init; } = new();
    public string? TextContains { get; init; }
    public bool MatchCase { get; init; }
    public bool IsAll => AllowedLevels.Count == 0 && string.IsNullOrEmpty(TextContains);

    public bool IsMatch(LogEntry entry)
    {
        if (IsAll)
            return true;

        if (AllowedLevels.Count > 0 && !AllowedLevels.Contains(entry.Level))
            return false;

        if (string.IsNullOrEmpty(TextContains))
            return true;

        var textContains = TextContains!;
        var comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        if (entry.Message.Contains(textContains, comparison))
            return true;

        if (!string.IsNullOrEmpty(entry.ExceptionText) &&
            entry.ExceptionText.Contains(textContains, comparison))
            return true;

        if (!string.IsNullOrEmpty(entry.Source) &&
            entry.Source.Contains(textContains, comparison))
            return true;

        foreach (var pair in entry.Properties)
        {
            if (!string.IsNullOrEmpty(pair.Key) &&
                pair.Key.Contains(textContains, comparison))
                return true;

            if (ValueContains(pair.Value, textContains, comparison))
                return true;
        }

        return false;
    }

    private static bool ValueContains(object? value, string target, StringComparison comparison)
    {
        if (value is null)
            return false;
        if (value is string text)
            return text.Contains(target, comparison);

        return value.ToString()?.Contains(target, comparison) == true;
    }
}
