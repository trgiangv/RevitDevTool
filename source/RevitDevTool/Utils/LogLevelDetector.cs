using Serilog.Events;
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.Utils;

public static class LogLevelDetector
{
    private const int MaxKeywordsPerLevel = 5;
    private const string MaxKeywordsPerLevelMessage = "Maximum 5 keywords allowed";
    private const char KeywordSeparator = ',';
    private static readonly PrefixRule[] PrefixRules =
    [
        new(LogEventLevel.Fatal, ["[FATAL]", "[FTL]", "[CRITICAL]", "[CRT]"]),
        new(LogEventLevel.Error, ["[ERROR]", "[ERR]"]),
        new(LogEventLevel.Warning, ["[WARNING]", "[WARN]", "[WRN]"]),
        new(LogEventLevel.Information, ["[INFO]", "[INF]", "[INFORMATION]"]),
        new(LogEventLevel.Debug, ["[DEBUG]", "[DBG]"]),
        new(LogEventLevel.Verbose, ["[TRACE]", "[TRC]", "[VERBOSE]", "[VRB]"])
    ];

    /// <summary>
    /// Validates keyword input string. Returns error message if invalid, null if valid.
    /// </summary>
    public static string? ValidateKeywords(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var keywords = input!
            .Split([KeywordSeparator], StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToArray();

        return keywords.Length > MaxKeywordsPerLevel
            ? MaxKeywordsPerLevelMessage
            : null;
    }

    public static LogEventLevel Detect(string? message, string[] criticalKeywords, string[] errorKeywords, string[] warningKeywords, string[] informationKeywords)
    {
        if (string.IsNullOrWhiteSpace(message))
            return LogEventLevel.Debug;

        var prefixLevel = DetectFromPrefix(message!);
        if (prefixLevel != LogEventLevel.Debug)
            return prefixLevel;

        if (ContainsAny(message!, criticalKeywords)) return LogEventLevel.Fatal;
        if (ContainsAny(message!, errorKeywords)) return LogEventLevel.Error;
        if (ContainsAny(message!, warningKeywords)) return LogEventLevel.Warning;
        if (ContainsAny(message!, informationKeywords)) return LogEventLevel.Information;
        return LogEventLevel.Debug;
    }

    public static string[] ParseKeywords(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        return input!.Split([','], StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.ToLowerInvariant())
            .Take(5)
            .ToArray();
    }

    private static LogEventLevel DetectFromPrefix(string message)
    {
        var start = FindFirstNonWhitespaceIndex(message);
        for (var i = 0; i < PrefixRules.Length; i++)
        {
            var rule = PrefixRules[i];
            for (var j = 0; j < rule.Prefixes.Length; j++)
            {
                if (StartsWithAt(message, start, rule.Prefixes[j]))
                {
                    return rule.Level;
                }
            }
        }

        return LogEventLevel.Debug;
    }

    private static int FindFirstNonWhitespaceIndex(string message)
    {
        var index = 0;
        while (index < message.Length && char.IsWhiteSpace(message[index]))
        {
            index++;
        }

        return index;
    }

    private static bool StartsWithAt(string text, int startIndex, string prefix)
    {
        return startIndex >= 0 &&
               startIndex + prefix.Length <= text.Length &&
               string.Compare(text, startIndex, prefix, 0, prefix.Length, StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static bool ContainsAny(string text, string[] keywords)
    {
        for (var i = 0; i < keywords.Length; i++)
        {
            if (text.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private readonly record struct PrefixRule(LogEventLevel Level, string[] Prefixes);
}
