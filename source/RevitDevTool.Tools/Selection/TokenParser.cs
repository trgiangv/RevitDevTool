using System.Diagnostics.CodeAnalysis;
using System.Globalization;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.Tools.Selection;

/// <summary>
/// Parse and format element identity tokens (ElementId / UniqueId / IfcGuid),
/// with optional <c>{linkInstanceId}@{inner}</c> scope prefix.
/// </summary>
public static class TokenParser
{
    public readonly record struct ParsedToken(TokenKind Kind, string Value, string? LinkInstanceId);

    /// <summary>
    /// Split optional <c>instanceId@</c> prefix, classify inner, apply
    /// <paramref name="defaultLinkInstanceId"/> only when the token is bare.
    /// </summary>
    public static ParsedToken? TryParse(string? token, string? defaultLinkInstanceId = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var value = token!;
        var linkInstanceId = defaultLinkInstanceId;
        if (TrySplit(token, out var splitLink, out var splitInner))
        {
            linkInstanceId = splitLink;
            value = splitInner!;
        }

        if (!TryClassifyInner(value.AsSpan(), out var kind))
            return null;

        return new ParsedToken(kind, value, linkInstanceId);
    }

    public static string FormatElementId(ElementId id)
    {
#if REVIT2024_OR_GREATER
        return id.Value.ToString(CultureInfo.InvariantCulture);
#else
        return id.IntegerValue.ToString(CultureInfo.InvariantCulture);
#endif
    }

    internal static bool TrySplit(
        string? token,
        [NotNullWhen(true)] out string? linkInstanceId,
        [NotNullWhen(true)] out string? inner)
    {
        if (!string.IsNullOrEmpty(token))
            return TrySplit(token.AsSpan(), out linkInstanceId, out inner);

        linkInstanceId = null;
        inner = null;
        return false;
    }

    internal static bool TrySplit(
        ReadOnlySpan<char> token,
        [NotNullWhen(true)] out string? linkInstanceId,
        [NotNullWhen(true)] out string? inner)
    {
        linkInstanceId = null;
        inner = null;

        var separator = token.IndexOf('@');
        if (separator <= 0)
            return false;
        if (token[(separator + 1)..].IndexOf('@') >= 0)
            return false;

        var instanceSpan = token[..separator];
        var innerSpan = token[(separator + 1)..];
        if (innerSpan.IsEmpty || !IsPositiveId(instanceSpan))
            return false;

        linkInstanceId = instanceSpan.ToString();
        inner = innerSpan.ToString();
        return true;
    }

    internal static bool TryClassifyInner(ReadOnlySpan<char> inner, out TokenKind kind)
    {
        switch (inner.Length)
        {
            case 45 when IsUniqueId(inner):
                kind = TokenKind.UniqueId;
                return true;
            case 22 when IsIfcGuid(inner):
                kind = TokenKind.IfcGuid;
                return true;
        }

        if (IsPositiveId(inner))
        {
            kind = TokenKind.ElementId;
            return true;
        }

        kind = default;
        return false;
    }

    internal static bool IsPositiveId(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return false;

        var valueText = text.ToString();
#if REVIT2024_OR_GREATER
        return long.TryParse(valueText, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0;
#else
        return int.TryParse(valueText, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0;
#endif
    }

    internal static bool TryParseElementId(string text, out ElementId id)
    {
        id = ElementId.InvalidElementId;
        if (!IsPositiveId(text.AsSpan()))
            return false;

#if REVIT2024_OR_GREATER
        id = new ElementId(long.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture));
#else
        id = new ElementId(int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture));
#endif
        return true;
    }

    internal static bool IsUniqueId(ReadOnlySpan<char> span)
    {
        if (span.Length != 45)
            return false;
        return span[8] == '-'
            && span[13] == '-'
            && span[18] == '-'
            && span[23] == '-'
            && span[36] == '-'
            && IsHexSegment(span, 0, 8)
            && IsHexSegment(span, 9, 4)
            && IsHexSegment(span, 14, 4)
            && IsHexSegment(span, 19, 4)
            && IsHexSegment(span, 24, 12)
            && IsHexSegment(span, 37, 8);
    }

    internal static bool IsIfcGuid(ReadOnlySpan<char> span)
    {
        if (span.Length != 22)
            return false;
        if (span[0] is not ('0' or '1' or '2' or '3'))
            return false;

        var hasDigitOrSpecial = false;
        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            switch (c)
            {
                case >= '0' and <= '9':
                case '_':
                case '$':
                    hasDigitOrSpecial = true;
                    break;
                case >= 'A' and <= 'Z':
                case >= 'a' and <= 'z':
                    break;
                default:
                    return false;
            }
        }

        return hasDigitOrSpecial;
    }

    private static bool IsHexSegment(ReadOnlySpan<char> span, int start, int length)
    {
        for (var i = 0; i < length; i++)
        {
            var c = span[start + i];
            var isHex = c is (>= '0' and <= '9')
                          or (>= 'a' and <= 'f')
                          or (>= 'A' and <= 'F');
            if (!isHex)
                return false;
        }

        return true;
    }
}
