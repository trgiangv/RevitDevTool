using Microsoft.Extensions.Logging;
using RevitDevTool.Logging;
using RevitDevTool.Models.Config;
using System.Diagnostics;
// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.Utils;

public static class TraceUtils
{
    private const int MaxKeywordsPerLevel = 5;
    private const string MaxKeywordsPerLevelMessage = "Maximum 5 keywords allowed";
    private const char KeywordSeparator = ',';

    private static LogFilterKeywords? _cachedKeywordsSource;
    private static string[]? _cachedCritical;
    private static string[]? _cachedError;
    private static string[]? _cachedWarning;
    private static string[]? _cachedInformation;

    public static void RegisterTraceListeners(bool includeWpfTrace, params TraceListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            if (listener == null || Trace.Listeners.Contains(listener)) continue;
            Trace.Listeners.Add(listener);
            if (includeWpfTrace && listener is AdapterTraceListener)
                PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        }
    }

    public static void UnregisterTraceListeners(bool includeWpfTrace, params TraceListener?[] listeners)
    {
        foreach (var listener in listeners)
        {
            if (listener == null || !Trace.Listeners.Contains(listener)) continue;
            Trace.Listeners.Remove(listener);
            if (includeWpfTrace && listener is AdapterTraceListener)
                PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
        }
    }

    /// <summary>
    /// Validates keyword input string.
    /// Returns error message if invalid, null if valid.
    /// </summary>
    public static string? ValidateKeywords(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var keywords = input!
#if NETCOREAPP
            .Split(KeywordSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
#else
            .Split([KeywordSeparator], StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim())
#endif
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToArray();

        return keywords.Length > MaxKeywordsPerLevel
            ? MaxKeywordsPerLevelMessage
            : null;
    }

    /// <summary>
    /// Detects LogLevel from message content using configured keywords.
    /// Checks prefix patterns first, then falls back to contains matching.
    /// </summary>
    public static LogLevel DetectLogLevel(string? message, LogFilterKeywords? keywords)
    {
        if (string.IsNullOrWhiteSpace(message))
            return LogLevel.Debug;
        
        var prefixLevel = DetectFromPrefix(message!);
        if (prefixLevel != LogLevel.Debug)
            return prefixLevel;

        keywords ??= new LogFilterKeywords();
        EnsureKeywordsCached(keywords);
        return DetectFromKeywordsCached(message!);
    }

    private static void EnsureKeywordsCached(LogFilterKeywords keywords)
    {
        if (ReferenceEquals(_cachedKeywordsSource, keywords))
            return;
        _cachedKeywordsSource = keywords;
        _cachedCritical = ParseAndLowerKeywords(keywords.Critical);
        _cachedError = ParseAndLowerKeywords(keywords.Error);
        _cachedWarning = ParseAndLowerKeywords(keywords.Warning);
        _cachedInformation = ParseAndLowerKeywords(keywords.Information);
    }

    private static string[]? ParseAndLowerKeywords(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

#if NETCOREAPP
        var keywords = input!.Split(KeywordSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
#else
        var keywords = input!.Split([KeywordSeparator], StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .ToArray();
#endif
        // Pre-lowercase all keywords once
        for (var i = 0; i < keywords.Length; i++)
            keywords[i] = keywords[i].ToLowerInvariant();

        return keywords.Length > 0 ? keywords : null;
    }

    private static LogLevel DetectFromKeywordsCached(string message)
    {
        if (ContainsAnyCached(message, _cachedCritical))
            return LogLevel.Critical;
        if (ContainsAnyCached(message, _cachedError))
            return LogLevel.Error;
        if (ContainsAnyCached(message, _cachedWarning))
            return LogLevel.Warning;
        if (ContainsAnyCached(message, _cachedInformation))
            return LogLevel.Information;
        return LogLevel.Debug;
    }

    private static bool ContainsAnyCached(string message, string[]? keywords)
    {
        if (keywords == null) return false;
        foreach (var keyword in keywords)
        {
            if (message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
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
        foreach (var prefix in prefixes)
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
