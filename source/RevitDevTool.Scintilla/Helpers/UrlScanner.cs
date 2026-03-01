namespace RevitDevTool.Scintilla.Helpers;

internal static class UrlScanner
{
    public static bool HasPotentialCandidate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch is ':' or '/' or '.' or '@')
                return true;
        }

        return false;
    }

    public static int FindAll(string text, List<(int Start, int Length)> output)
    {
        output.Clear();
        if (string.IsNullOrEmpty(text) || !HasPotentialCandidate(text))
            return 0;

        var span = text.AsSpan();
        var cursor = 0;
        while (cursor < span.Length)
        {
            if (!TryFindUrl(span.Slice(cursor), out var localStart, out var length) || length <= 0)
                break;

            var absoluteStart = cursor + localStart;
            output.Add((absoluteStart, length));
            cursor = absoluteStart + length;
        }

        return output.Count;
    }

    public static bool TryNormalizeUri(string candidate, out string targetUri)
    {
        targetUri = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var trimmed = candidate.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute))
        {
            targetUri = absolute.ToString();
            return true;
        }

        if (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase) &&
            Uri.TryCreate("https://" + trimmed, UriKind.Absolute, out var wwwUri))
        {
            targetUri = wwwUri.ToString();
            return true;
        }

        return false;
    }

    private static bool TryFindUrl(ReadOnlySpan<char> text, out int start, out int length)
    {
        start = 0;
        length = 0;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == ':' && TryFindSchemeUrl(text, i, out start, out length))
            {
                return true;
            }

            if (ch == 'w' && TryFindWwwUrl(text, i, out start, out length))
            {
                return true;
            }

            if (ch == '@' && TryFindEmail(text, i, out start, out length))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindSchemeUrl(ReadOnlySpan<char> text, int colonIndex, out int start, out int length)
    {
        start = 0;
        length = 0;

        if (colonIndex + 2 >= text.Length || text[colonIndex + 1] != '/' || text[colonIndex + 2] != '/')
            return false;

        var urlStart = FindUrlStart(text, colonIndex);
        var urlEnd = FindUrlEnd(text, urlStart);
        if (urlEnd <= urlStart)
            return false;

        start = urlStart;
        length = urlEnd - urlStart;
        return true;
    }

    private static bool TryFindWwwUrl(ReadOnlySpan<char> text, int index, out int start, out int length)
    {
        start = 0;
        length = 0;

        if (index + 4 >= text.Length ||
            text[index + 1] != 'w' ||
            text[index + 2] != 'w' ||
            text[index + 3] != '.' ||
            !char.IsLetter(text[index + 4]))
        {
            return false;
        }

        var urlEnd = FindUrlEnd(text, index);
        if (urlEnd <= index)
            return false;

        start = index;
        length = urlEnd - index;
        return true;
    }

    private static bool TryFindEmail(ReadOnlySpan<char> text, int atIndex, out int start, out int length)
    {
        start = 0;
        length = 0;

        if (atIndex <= 0 || atIndex + 3 >= text.Length)
            return false;

        var emailStart = atIndex;
        while (emailStart > 0 && IsEmailChar(text[emailStart - 1]))
            emailStart--;

        var emailEnd = atIndex + 1;
        while (emailEnd < text.Length && IsEmailChar(text[emailEnd]))
            emailEnd++;

        if (emailEnd <= atIndex + 1 || !HasDotAfterAt(text, atIndex, emailEnd))
            return false;

        start = emailStart;
        length = emailEnd - emailStart;
        return true;
    }

    private static bool HasDotAfterAt(ReadOnlySpan<char> text, int atIndex, int endExclusive)
    {
        for (var i = atIndex + 1; i < endExclusive; i++)
        {
            if (text[i] == '.')
                return true;
        }

        return false;
    }

    private static int FindUrlStart(ReadOnlySpan<char> text, int colonPos)
    {
        var start = colonPos;
        while (start > 0)
        {
            var prev = text[start - 1];
            if (!char.IsLetterOrDigit(prev) && prev != '+' && prev != '-' && prev != '.')
                break;
            start--;
        }

        return start;
    }

    private static int FindUrlEnd(ReadOnlySpan<char> text, int start)
    {
        var end = start;
        while (end < text.Length)
        {
            var ch = text[end];
            if (ch is ' ' or '\r' or '\n' or '\t' or '<' or '>' or '"' or '\'' or '[' or ']')
                break;

            if (ch == '\r' || ch == '\n')
                break;

            end++;
        }

        while (end > start)
        {
            var last = text[end - 1];
            if (last is '.' or ',' or ';' or ':' or '!' or '?')
                end--;
            else
                break;
        }

        return end;
    }

    private static bool IsEmailChar(char ch)
        => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' or '+';
}
