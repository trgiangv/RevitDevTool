using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Core;
namespace RevitDevTool.Scintilla.Formatting;

/// <summary>
/// Unified parser for the <c>[HH:mm:ss.fff LVL] </c> prefix produced by <see cref="ScintillaLogFormatter"/>.
/// Operates on both UTF-8 byte spans and UTF-16 strings, eliminating the previous three
/// separate parsing methods that duplicated the same logic.
/// </summary>
internal readonly struct LogPrefixParseResult
{
    public static LogPrefixParseResult NotFound { get; } = default;

    public LogPrefixParseResult(int prefixByteLength, int timestampLength, int levelByteStart)
    {
        Found = true;
        PrefixByteLength = prefixByteLength;
        TimestampLength = timestampLength;
        LevelByteStart = levelByteStart;
    }

    public bool Found { get; }
    public int PrefixByteLength { get; }
    public int TimestampLength { get; }
    public int LevelByteStart { get; }
}

internal static class LogPrefixParser
{
    /// <summary>
    /// Parses the <c>[timestamp LVL] </c> prefix from a UTF-8 byte span.
    /// </summary>
    public static LogPrefixParseResult TryParse(ReadOnlySpan<byte> utf8Line)
    {
        if (utf8Line.Length < 8 || utf8Line[0] != (byte)'[')
            return LogPrefixParseResult.NotFound;

        var closeIndex = -1;
        for (var i = 1; i + 1 < utf8Line.Length; i++)
        {
            if (utf8Line[i] == (byte)']' && utf8Line[i + 1] == (byte)' ')
            {
                closeIndex = i;
                break;
            }
        }

        if (closeIndex <= 2)
            return LogPrefixParseResult.NotFound;

        var split = closeIndex - 4;
        if (split <= 1 || utf8Line[split] != (byte)' ')
            return LogPrefixParseResult.NotFound;

        var timestampLength = split - 1;
        if (timestampLength <= 0)
            return LogPrefixParseResult.NotFound;

        return new LogPrefixParseResult(
            prefixByteLength: closeIndex + 2,
            timestampLength: timestampLength,
            levelByteStart: split + 1);
    }

    /// <summary>
    /// Parses the <c>[timestamp LVL] </c> prefix from a UTF-16 string.
    /// Returns the parse result and the remainder message after the prefix.
    /// </summary>
    public static LogPrefixParseResult TryParse(string message, out string remainderMessage)
    {
        remainderMessage = message;
        if (string.IsNullOrEmpty(message) || message[0] != '[')
            return LogPrefixParseResult.NotFound;

        var closeIndex = message.IndexOf("] ", StringComparison.Ordinal);
        if (closeIndex <= 4)
            return LogPrefixParseResult.NotFound;

        var split = -1;
        for (var i = closeIndex - 1; i >= 1; i--)
        {
            if (message[i] == ' ')
            {
                split = i;
                break;
            }
        }

        if (split <= 1 || closeIndex - split != 4)
            return LogPrefixParseResult.NotFound;

        var timestampLength = split - 1;
        if (timestampLength <= 0)
            return LogPrefixParseResult.NotFound;

        remainderMessage = closeIndex + 2 < message.Length
            ? message.Substring(closeIndex + 2)
            : string.Empty;

        var prefixByteLength = GetUtf8ByteCount(message, 0, closeIndex + 2);

        return new LogPrefixParseResult(
            prefixByteLength: prefixByteLength,
            timestampLength: timestampLength,
            levelByteStart: split + 1);
    }

    /// <summary>
    /// Builds prefix <see cref="RenderSegment"/>s from a parsed UTF-8 prefix.
    /// </summary>
    public static void BuildPrefixSegments(
        LogPrefixParseResult result,
        ReadOnlySpan<byte> utf8Line,
        LogLevel fallbackLevel,
        IList<RenderSegment> segments)
    {
        segments.Add(new RenderSegment(1, LogSemanticStyle.Punctuation));
        segments.Add(new RenderSegment(result.TimestampLength, LogSemanticStyle.SecondaryText));
        segments.Add(new RenderSegment(1, LogSemanticStyle.SecondaryText));
        segments.Add(new RenderSegment(3, ParseShortLevel(
            utf8Line[result.LevelByteStart],
            utf8Line[result.LevelByteStart + 1],
            utf8Line[result.LevelByteStart + 2],
            fallbackLevel)));
        segments.Add(new RenderSegment(2, LogSemanticStyle.Punctuation));

        var remainder = utf8Line.Length - result.PrefixByteLength;
        if (remainder > 0)
            segments.Add(new RenderSegment(remainder, LogSemanticStyle.Text));
    }

    /// <summary>
    /// Builds prefix <see cref="RenderSegment"/>s from a parsed string prefix.
    /// </summary>
    public static void BuildPrefixSegments(
        LogPrefixParseResult result,
        string message,
        LogLevel fallbackLevel,
        IList<RenderSegment> segments)
    {
        var levelStart = result.LevelByteStart;
        segments.Add(new RenderSegment(1, LogSemanticStyle.Punctuation));
        segments.Add(new RenderSegment(GetUtf8ByteCount(message, 1, result.TimestampLength), LogSemanticStyle.SecondaryText));
        segments.Add(new RenderSegment(1, LogSemanticStyle.SecondaryText));

        var levelStyle = levelStart >= 0 && levelStart + 2 < message.Length
            ? ParseShortLevel((byte)message[levelStart], (byte)message[levelStart + 1], (byte)message[levelStart + 2], fallbackLevel)
            : GetLevelSemanticStyle(fallbackLevel);

        segments.Add(new RenderSegment(GetUtf8ByteCount(message, levelStart, 3), levelStyle));
        segments.Add(new RenderSegment(2, LogSemanticStyle.Punctuation));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static LogSemanticStyle ParseShortLevel(byte b0, byte b1, byte b2, LogLevel fallbackLevel) =>
        (b0, b1, b2) switch
        {
            ((byte)'T', (byte)'R', (byte)'C') => LogSemanticStyle.LevelTrace,
            ((byte)'D', (byte)'B', (byte)'G') => LogSemanticStyle.LevelDebug,
            ((byte)'I', (byte)'N', (byte)'F') => LogSemanticStyle.LevelInformation,
            ((byte)'W', (byte)'R', (byte)'N') => LogSemanticStyle.LevelWarning,
            ((byte)'E', (byte)'R', (byte)'R') => LogSemanticStyle.LevelError,
            ((byte)'C', (byte)'R', (byte)'T') => LogSemanticStyle.LevelCritical,
            _ => GetLevelSemanticStyle(fallbackLevel)
        };

    internal static LogSemanticStyle GetLevelSemanticStyle(LogLevel level) => level switch
    {
        LogLevel.Trace => LogSemanticStyle.LevelTrace,
        LogLevel.Debug => LogSemanticStyle.LevelDebug,
        LogLevel.Information => LogSemanticStyle.LevelInformation,
        LogLevel.Warning => LogSemanticStyle.LevelWarning,
        LogLevel.Error => LogSemanticStyle.LevelError,
        LogLevel.Critical => LogSemanticStyle.LevelCritical,
        _ => LogSemanticStyle.LevelInformation
    };

    private static int GetUtf8ByteCount(string source, int start, int length)
    {
#if NET8_0_OR_GREATER
        return Encoding.UTF8.GetByteCount(source.AsSpan(start, length));
#else
        return Encoding.UTF8.GetByteCount(source.Substring(start, length));
#endif
    }
}
