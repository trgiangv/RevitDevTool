using Microsoft.Extensions.Logging;
using RevitDevTool.Logger.Config;
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable LoopCanBeConvertedToQuery

namespace RevitDevTool.Logger.Listeners;

public static class LogLevelDetector
{
    private const int MaxKeywordsPerLevel = 5;
    private const string MaxKeywordsPerLevelMessage = "Maximum 5 keywords allowed";
    private const char KeywordSeparator = ',';

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

    private static LogFilterKeywords? _cachedKeywordsSource;
    private static string[] _cachedCritical = Array.Empty<string>();
    private static string[] _cachedError = Array.Empty<string>();
    private static string[] _cachedWarning = Array.Empty<string>();
    private static string[] _cachedInformation = Array.Empty<string>();

    /// <summary>
    /// Detects LogLevel from message content using configured keywords, with internal caching.
    /// </summary>
    public static LogLevel DetectLogLevel(string? message, LogFilterKeywords? keywords)
    {
        keywords ??= new LogFilterKeywords();
        EnsureKeywordsCached(keywords);
        return Detect(message, _cachedCritical, _cachedError, _cachedWarning, _cachedInformation);
    }

    public static LogLevel Detect(string? message, LogFilterKeywords? keywords)
    {
        var filter = keywords ?? new LogFilterKeywords();
        return Detect(
            message,
            ParseKeywords(filter.Critical),
            ParseKeywords(filter.Error),
            ParseKeywords(filter.Warning),
            ParseKeywords(filter.Information));
    }

    private static void EnsureKeywordsCached(LogFilterKeywords keywords)
    {
        if (ReferenceEquals(_cachedKeywordsSource, keywords))
            return;
        _cachedKeywordsSource = keywords;
        _cachedCritical = ParseKeywords(keywords.Critical);
        _cachedError = ParseKeywords(keywords.Error);
        _cachedWarning = ParseKeywords(keywords.Warning);
        _cachedInformation = ParseKeywords(keywords.Information);
    }

    public static LogLevel Detect(string? message, string[] criticalKeywords, string[] errorKeywords, string[] warningKeywords, string[] informationKeywords)
    {
        if (string.IsNullOrWhiteSpace(message))
            return LogLevel.Debug;

        var prefixLevel = DetectFromPrefix(message!);
        if (prefixLevel != LogLevel.Debug)
            return prefixLevel;

        var lower = message!.ToLowerInvariant();
        if (ContainsAny(lower, criticalKeywords)) return LogLevel.Critical;
        if (ContainsAny(lower, errorKeywords)) return LogLevel.Error;
        if (ContainsAny(lower, warningKeywords)) return LogLevel.Warning;
        if (ContainsAny(lower, informationKeywords)) return LogLevel.Information;
        return LogLevel.Debug;
    }

    public static string[] ParseKeywords(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Array.Empty<string>();
        var safeInput = input ?? string.Empty;
        return safeInput.Split([','], StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.ToLowerInvariant())
            .Take(5)
            .ToArray();
    }

    private static LogLevel DetectFromPrefix(string message)
    {
        var trimmed = message.TrimStart();
        if (StartsWithAny(trimmed, "[FATAL]", "[FTL]", "[CRITICAL]", "[CRT]"))
            return LogLevel.Critical;
        if (StartsWithAny(trimmed, "[ERROR]", "[ERR]"))
            return LogLevel.Error;
        if (StartsWithAny(trimmed, "[WARNING]", "[WARN]", "[WRN]"))
            return LogLevel.Warning;
        if (StartsWithAny(trimmed, "[INFO]", "[INF]", "[INFORMATION]"))
            return LogLevel.Information;
        if (StartsWithAny(trimmed, "[DEBUG]", "[DBG]"))
            return LogLevel.Debug;
        if (StartsWithAny(trimmed, "[TRACE]", "[TRC]", "[VERBOSE]", "[VRB]"))
            return LogLevel.Trace;
        return LogLevel.Debug;
    }

    private static bool StartsWithAny(string text, params string[] prefixes)
    {
        for (var i = 0; i < prefixes.Length; i++)
        {
            if (text.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool ContainsAny(string text, string[] keywords)
    {
        for (var i = 0; i < keywords.Length; i++)
        {
            if (text.Contains(keywords[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
