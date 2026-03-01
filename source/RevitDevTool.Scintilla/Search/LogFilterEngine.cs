using System.Text;
using RevitDevTool.Scintilla.Core;

namespace RevitDevTool.Scintilla.Search;

internal sealed class LogFilterState
{
    public static readonly LogFilterState All = new(
        LogFilterOptions.All,
        null,
        null,
        normalizedTextContains: null,
        levelMask: 0,
        hasLevelFilter: false,
        hasTextFilter: false);

    public LogFilterState(
        LogFilterOptions filter,
        byte[]? matchCaseFilterUtf8,
        byte[]? ignoreCaseAsciiFilterUtf8,
        string? normalizedTextContains,
        ulong levelMask,
        bool hasLevelFilter,
        bool hasTextFilter)
    {
        Filter = filter;
        MatchCaseFilterUtf8 = matchCaseFilterUtf8;
        IgnoreCaseAsciiFilterUtf8 = ignoreCaseAsciiFilterUtf8;
        NormalizedTextContains = normalizedTextContains;
        LevelMask = levelMask;
        HasLevelFilter = hasLevelFilter;
        HasTextFilter = hasTextFilter;
    }

    public LogFilterOptions Filter { get; }
    public byte[]? MatchCaseFilterUtf8 { get; }
    public byte[]? IgnoreCaseAsciiFilterUtf8 { get; }
    public string? NormalizedTextContains { get; }
    public ulong LevelMask { get; }
    public bool HasLevelFilter { get; }
    public bool HasTextFilter { get; }
}

internal static class LogFilterEngine
{
    public static bool AreEquivalent(LogFilterOptions left, LogFilterOptions right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left.MatchCase != right.MatchCase)
            return false;

        if (!string.Equals(left.TextContains, right.TextContains, StringComparison.Ordinal))
            return false;

        return BuildLevelMask(left) == BuildLevelMask(right);
    }

    public static LogFilterState CreateState(LogFilterOptions? filterOptions)
    {
        var filter = filterOptions ?? LogFilterOptions.All;
        var levelMask = BuildLevelMask(filter);
        var hasLevelFilter = filter.AllowedLevels.Count > 0;
        var textContains = filter.TextContains;
        if (string.IsNullOrEmpty(textContains))
            return new LogFilterState(filter, null, null, null, levelMask, hasLevelFilter, hasTextFilter: false);

        if (filter.MatchCase)
            return new LogFilterState(filter, Encoding.UTF8.GetBytes(textContains), null, null, levelMask, hasLevelFilter, hasTextFilter: true);

        if (IsAscii(textContains!))
            return new LogFilterState(filter, null, Encoding.UTF8.GetBytes(textContains), null, levelMask, hasLevelFilter, hasTextFilter: true);

        return new LogFilterState(filter, null, null, textContains!.ToUpperInvariant(), levelMask, hasLevelFilter, hasTextFilter: true);
    }

    public static bool IsMatch(LogEntry entry, LogFilterState state)
    {
        var filter = state.Filter;
        if (filter.IsAll)
            return true;

        if (!MatchesLevel(entry, state))
            return false;

        if (!state.HasTextFilter)
            return true;

        var textContains = filter.TextContains!;
        var comparison = filter.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        if (MatchesMessage(entry, state, filter, textContains, comparison))
            return true;

        return MatchesSourceExceptionOrProperties(entry, state, textContains, comparison);
    }

    public static bool IsLevelMatchOnly(LogEntry entry, LogFilterState state)
        => MatchesLevel(entry, state);

    private static bool MatchesLevel(LogEntry entry, LogFilterState state)
    {
        if (!state.HasLevelFilter)
            return true;

        var levelValue = (int)entry.Level;
        if ((uint)levelValue > 63u)
            return false;

        return (state.LevelMask & (1UL << levelValue)) != 0;
    }

    private static bool MatchesSourceExceptionOrProperties(
        LogEntry entry,
        LogFilterState state,
        string textContains,
        StringComparison comparison)
    {
        var metadataText = entry.GetOrCreateMetadataSearchText();
        if (metadataText.Length == 0)
            return false;

        if (comparison == StringComparison.Ordinal)
        {
            return metadataText.IndexOf(textContains, StringComparison.Ordinal) >= 0;
        }

        var normalizedNeedle = state.NormalizedTextContains;
        if (!string.IsNullOrEmpty(normalizedNeedle))
        {
            var normalizedMetadata = entry.GetOrCreateMetadataSearchTextUpperInvariant();
            return normalizedMetadata.IndexOf(normalizedNeedle, StringComparison.Ordinal) >= 0;
        }

        return metadataText.IndexOf(textContains, comparison) >= 0;
    }

    private static bool MatchesMessage(
        LogEntry entry,
        LogFilterState state,
        LogFilterOptions filter,
        string textContains,
        StringComparison comparison)
    {
        if (entry.Message.Array is null || entry.Message.Count <= 0)
            return false;

        if (filter.MatchCase && state.MatchCaseFilterUtf8 is { Length: > 0 } matchCase)
            return ContainsBytes(entry.Message, matchCase);

        if (!filter.MatchCase && state.IgnoreCaseAsciiFilterUtf8 is { Length: > 0 } ignoreCaseAscii)
            return ContainsAsciiCaseInsensitiveBytes(entry.Message, ignoreCaseAscii);

        var message = entry.GetOrCreateMessageText();
        if (comparison == StringComparison.Ordinal)
            return message.IndexOf(textContains, StringComparison.Ordinal) >= 0;

        var normalizedNeedle = state.NormalizedTextContains;
        if (!string.IsNullOrEmpty(normalizedNeedle))
        {
            var normalizedMessage = entry.GetOrCreateMessageTextUpperInvariant();
            return normalizedMessage.IndexOf(normalizedNeedle, StringComparison.Ordinal) >= 0;
        }

        return message.IndexOf(textContains, comparison) >= 0;
    }

    private static bool ContainsBytes(ArraySegment<byte> source, byte[] target)
    {
        if (target.Length == 0)
            return true;

        if (source.Array is null || source.Count < target.Length)
            return false;

#if NET8_0_OR_GREATER
        var sourceSpan = new ReadOnlySpan<byte>(source.Array, source.Offset, source.Count);
        return sourceSpan.IndexOf(target) >= 0;
#else
        var sourceArray = source.Array;
        var sourceStart = source.Offset;
        var sourceEnd = source.Offset + source.Count - target.Length;
        for (var i = sourceStart; i <= sourceEnd; i++)
        {
            var matched = true;
            for (var j = 0; j < target.Length; j++)
            {
                if (sourceArray[i + j] == target[j])
                    continue;

                matched = false;
                break;
            }

            if (matched)
                return true;
        }

        return false;
#endif
    }

    private static bool ContainsAsciiCaseInsensitiveBytes(ArraySegment<byte> source, byte[] target)
    {
        if (target.Length == 0)
            return true;

        if (source.Array is null || source.Count < target.Length)
            return false;

        var sourceArray = source.Array;
        var sourceStart = source.Offset;
        var sourceEnd = source.Offset + source.Count - target.Length;
        for (var i = sourceStart; i <= sourceEnd; i++)
        {
            var matched = true;
            for (var j = 0; j < target.Length; j++)
            {
                if (ToLowerAscii(sourceArray[i + j]) == ToLowerAscii(target[j]))
                    continue;

                matched = false;
                break;
            }

            if (matched)
                return true;
        }

        return false;
    }

    private static bool IsAscii(string value)
    {
#if NET8_0_OR_GREATER
        return Ascii.IsValid(value);
#else
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] > 127)
                return false;
        }

        return true;
#endif
    }

    private static byte ToLowerAscii(byte value)
        => value is >= (byte)'A' and <= (byte)'Z' ? (byte)(value + 32) : value;

    private static ulong BuildLevelMask(LogFilterOptions filter)
    {
        if (filter.AllowedLevels.Count == 0)
            return 0;

        var mask = 0UL;
        foreach (var level in filter.AllowedLevels)
        {
            var levelValue = (int)level;
            if ((uint)levelValue <= 63u)
                mask |= 1UL << levelValue;
        }

        return mask;
    }
}
