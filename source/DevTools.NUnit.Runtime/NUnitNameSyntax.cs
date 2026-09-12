using System.Text;

namespace DevTools.NUnit.Runtime;

/// <summary>
/// Character walks over NUnit display strings (<c>ITest.FullName</c> is not
/// a CLR type name). Last-dot matches NUnit MTP <c>FindLastDotNotInParens</c>:
/// reverse scan, depth only on <c>( )</c>. Quote-aware walks apply only when
/// stripping numeric suffixes or compacting argument whitespace.
/// </summary>
internal static class NUnitNameSyntax
{
    public const char ArgOpen = '(';
    public const char ArgClose = ')';
    public const char GenericOpen = '<';
    public const char GenericClose = '>';
    public const char MemberDot = '.';
    public const char NestedType = '+';
    public const char Arity = '`';
    public const char TypeArgComma = ',';
    public const char Quote = '"';
    public const char Escape = '\\';
    public const char RegexStart = '^';
    public const char RegexEnd = '$';

    public static int ArgDepth(int depth, char c) => c switch
    {
        ArgOpen => depth + 1,
        ArgClose when depth > 0 => depth - 1,
        _ => depth
    };

    public static int GenericDepth(int depth, char c) => c switch
    {
        GenericOpen => depth + 1,
        GenericClose when depth > 0 => depth - 1,
        _ => depth
    };

    public static bool ContainsAtDepthZero(string value, char symbol)
    {
        var depth = 0;
        foreach (var c in value)
        {
            if (depth == 0 && c == symbol)
                return true;
            depth = ArgDepth(depth, c);
        }

        return false;
    }

    public static int LastAtDepthZero(string value, char separator)
    {
        var depth = 0;
        for (var index = value.Length - 1; index >= 0; index--)
        {
            var c = value[index];
            if (c == ArgClose)
                depth++;
            else if (c == ArgOpen)
                depth--;
            else if (c == separator && depth == 0)
                return index;
        }

        return -1;
    }

    public static bool TrySplitLast(string value, char separator, out string left, out string right)
    {
        var index = LastAtDepthZero(value, separator);
        if (index < 0)
        {
            left = value;
            right = value;
            return false;
        }

        left = value[..index];
        right = value[(index + 1)..];
        return true;
    }

    public static string Qualify(string? namespaceName, string name) =>
        string.IsNullOrEmpty(namespaceName) ? name : namespaceName + MemberDot + name;

    public static string Nested(string declaring, string nested) =>
        declaring + NestedType + nested;

    public static string Anchored(string pattern) =>
        string.Concat(RegexStart, pattern, RegexEnd);

    public static string TrimEmptyParentheses(string value)
    {
        while (value.Length >= 2
               && value[^2] == ArgOpen
               && value[^1] == ArgClose)
            value = value[..^2];
        return value;
    }

    public static string StripArgumentLists(string value)
    {
        var depth = 0;
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case ArgOpen:
                    depth++;
                    continue;
                case ArgClose when depth > 0:
                    depth--;
                    continue;
            }

            if (depth == 0)
                builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// First <c>(</c> that is not inside <c>&lt;T&gt;</c> — fixture ctor args
    /// after a closed generic, not a generic argument list.
    /// </summary>
    public static int IndexOfArgList(string typeName)
    {
        var depth = 0;
        for (var index = 0; index < typeName.Length; index++)
        {
            var c = typeName[index];
            if (c == ArgOpen && depth == 0)
                return index;
            depth = GenericDepth(depth, c);
        }

        return -1;
    }

    public static string Canonicalize(string value) =>
        CompactArgWhitespace(TrimEmptyParentheses(StripNumericTypeSuffixes(value)));

    public static bool Same(string? left, string? right)
    {
        if (left is not { Length: > 0 } || right is not { Length: > 0 })
            return false;
        return string.Equals(left, right, StringComparison.Ordinal) 
               || string.Equals(Canonicalize(left), Canonicalize(right), StringComparison.Ordinal);
    }

    public static bool Identifies(string? testId, string? fullName, string? name) =>
        Same(testId, name) || Same(fullName, name);

    public static string StripNumericTypeSuffixes(string value)
    {
        if (value.IndexOf(ArgOpen) < 0)
            return value;

        var builder = new StringBuilder(value.Length);
        var quote = new QuoteScan();
        for (var index = 0; index < value.Length; index++)
        {
            if (quote.Append(builder, value[index]))
                continue;
            if (TrySkipNumericSuffix(value, index, out var skip))
                index += skip - 1;
            else
                builder.Append(value[index]);
        }

        return builder.ToString();
    }

    private static string CompactArgWhitespace(string value)
    {
        if (value.IndexOf(ArgOpen) < 0)
            return value;

        var builder = new StringBuilder(value.Length);
        var depth = 0;
        var quote = new QuoteScan();
        foreach (var c in value)
        {
            if (quote.Append(builder, c))
                continue;
            depth = ArgDepth(depth, c);
            if (depth == 0 || !char.IsWhiteSpace(c))
                builder.Append(c);
        }

        return builder.ToString();
    }

    private static bool TrySkipNumericSuffix(string value, int index, out int skip)
    {
        skip = 0;
        if (index == 0 || !char.IsDigit(value[index - 1]))
            return false;

        if (StartsAt(value, index, "UL"))
            skip = 2;
        else if (value[index] is 'd' or 'f' or 'm' or 'L')
            skip = 1;
        else
            return false;

        var next = index + skip;
        return next == value.Length || value[next] is ',' or ')';
    }

    private static bool StartsAt(string value, int index, string token)
    {
        if (index + token.Length > value.Length)
            return false;
        return string.CompareOrdinal(value, index, token, 0, token.Length) == 0;
    }

    private struct QuoteScan
    {
        private bool _inString;
        private bool _escape;

        public bool Append(StringBuilder builder, char c)
        {
            if (!_inString)
            {
                if (c != Quote)
                    return false;
                _inString = true;
                builder.Append(c);
                return true;
            }

            builder.Append(c);
            if (_escape)
            {
                _escape = false;
                return true;
            }

            _escape = c == Escape;
            if (c == Quote)
                _inString = false;
            return true;
        }
    }
}
