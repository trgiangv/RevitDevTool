namespace DevTools.NUnit.Runtime;

/// <summary>
/// NUnit <c>ITest.FullName</c> is a display string, not a CLR type name.
/// Last-dot matches NUnit MTP <c>FindLastDotNotInParens</c>: scan from the
/// end, depth only on <c>( )</c>. Display type args are unqualified
/// (<c>Int32</c>, not <c>System.Int32</c>), so <c>&lt; &gt;</c> is not a
/// last-dot fence. Walk generics only for arity and ctor-args after
/// <c>Foo&lt;T&gt;(...)</c>.
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

    public static string Qualify(string? namespaceName, string name) =>
        string.IsNullOrEmpty(namespaceName) ? name : namespaceName + MemberDot + name;

    public static string Nested(string declaring, string nested) =>
        declaring + NestedType + nested;

    public static string Anchored(string pattern) =>
        string.Concat(RegexStart, pattern, RegexEnd);
}
