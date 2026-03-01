using System.Globalization;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Helpers;
namespace RevitDevTool.Scintilla.Formatting;

internal sealed class DisplayValueFormatter
{
    private readonly TokenResolutionEngine _tokenResolution;
    private readonly UrlSegmentHelper _urlHelper = new();

    public DisplayValueFormatter(TokenResolutionEngine tokenResolution)
    {
        _tokenResolution = tokenResolution;
    }

    public bool TryAppendSegments(LogRenderContext context, string message, IList<RenderSegment> segments)
    {
        if (string.IsNullOrEmpty(message))
            return false;

        var writer = new RenderTokenWriter(segments);
        if (TryAppendStructuredTypeNameSegments(context, message, writer))
            return true;

        if (TryAppendObjectLikeSegments(context, message, writer))
            return true;

        AppendTokenAwareSegments(context, message, writer);
        return true;
    }

    public void AppendMessageSegments(LogRenderContext context, string message, IList<RenderSegment> segments)
        => TryAppendSegments(context, message, segments);

    private void AppendTokenAwareSegments(LogRenderContext context, string message, RenderTokenWriter writer)
    {
        _urlHelper.AppendUrlAwareSegments(
            message, 0, message.Length,
            LogSemanticStyle.Text,
            writer,
            (text, start, length, w) => AppendNonUrlTokenAwareSegments(context, text, start, length, w));
    }

    private void AppendNonUrlTokenAwareSegments(
        LogRenderContext context,
        string message,
        int startOffset,
        int length,
        RenderTokenWriter writer)
    {
        if (length <= 0)
            return;

        if (CanAppendAsPlainText(context))
        {
            writer.Add(message, startOffset, length, LogSemanticStyle.Text);
            return;
        }

        var endOffset = startOffset + length;
        var cursor = startOffset;
        while (cursor < endOffset)
        {
            var separatorIndex = FindNextSeparator(message, cursor, endOffset);
            if (separatorIndex < 0)
            {
                AppendTokenCandidate(context, message, cursor, endOffset - cursor, writer);
                break;
            }

            if (separatorIndex > cursor)
                AppendTokenCandidate(context, message, cursor, separatorIndex - cursor, writer);

            writer.Add(message, separatorIndex, 1, LogSemanticStyle.Punctuation);
            cursor = separatorIndex + 1;
        }
    }

    private void AppendTokenCandidate(
        LogRenderContext context,
        string message,
        int start,
        int length,
        RenderTokenWriter writer,
        LogSemanticStyle fallbackSemanticStyle = LogSemanticStyle.Text)
    {
        if (length <= 0)
            return;

        if (!_tokenResolution.HasResolvers)
        {
            writer.Add(message, start, length, fallbackSemanticStyle);
            return;
        }

        var tokenText = message.Substring(start, length);

        if (_tokenResolution.TryResolveToken(context, tokenText, start, length, writer))
            return;

        if (fallbackSemanticStyle == LogSemanticStyle.Text && PayloadFormattingHelpers.IsStructuredPayloadTypeToken(context, tokenText))
        {
            writer.Add(tokenText, LogSemanticStyle.JsonString);
            return;
        }

        writer.Add(tokenText, fallbackSemanticStyle);
    }

    private bool TryAppendObjectLikeSegments(LogRenderContext context, string message, RenderTokenWriter writer)
    {
        if (!LooksLikeObjectLiteral(message))
            return false;

        var expectingValue = false;
        var i = 0;
        while (i < message.Length)
        {
            if (TryAppendObjectWhitespace(message, writer, ref i) ||
                TryAppendObjectPunctuation(message, writer, ref i, ref expectingValue) ||
                TryAppendAssignment(message, writer, ref i, ref expectingValue))
            {
                continue;
            }

            var tokenStart = i;
            i = ReadObjectTokenEnd(message, i);
            var tokenLength = i - tokenStart;
            if (tokenLength <= 0)
                continue;

            if (!expectingValue && NextNonWhitespaceEquals(message, i, '='))
            {
                writer.Add(message, tokenStart, tokenLength, LogSemanticStyle.JsonKey);
                continue;
            }

            AppendTokenCandidate(
                context,
                message,
                tokenStart,
                tokenLength,
                writer,
                GetObjectLiteralValueStyle(message, tokenStart, tokenLength));
        }

        return true;
    }

    private bool TryAppendStructuredTypeNameSegments(
        LogRenderContext context,
        string message,
        RenderTokenWriter writer)
    {
        var spans = CollectStructuredTypeNameSpans(context, message);
        if (spans.Count == 0)
            return false;

        spans.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        AppendStructuredTypeSpans(context, message, spans, writer);

        return true;
    }

    private static LogSemanticStyle GetObjectLiteralValueStyle(string message, int start, int length)
    {
        if (length <= 0 || start < 0 || start + length > message.Length)
            return LogSemanticStyle.JsonString;

        if (EqualsIgnoreCaseAscii(message, start, length, "true") ||
            EqualsIgnoreCaseAscii(message, start, length, "false"))
            return LogSemanticStyle.JsonBoolean;
        if (EqualsIgnoreCaseAscii(message, start, length, "null"))
            return LogSemanticStyle.JsonNull;
        if (IsLikelyNumberToken(message, start, length))
            return LogSemanticStyle.JsonNumber;
        return LogSemanticStyle.JsonString;
    }

    private static bool EqualsIgnoreCaseAscii(string text, int start, int length, string expectedLower)
    {
        if (length != expectedLower.Length)
            return false;

        for (var i = 0; i < length; i++)
        {
            var ch = text[start + i];
            if (ch is >= 'A' and <= 'Z')
                ch = (char)(ch + 32);
            if (ch != expectedLower[i])
                return false;
        }

        return true;
    }

    private static bool IsLikelyNumberToken(string text, int start, int length)
    {
        if (length <= 0 || start < 0 || start + length > text.Length)
            return false;

        var token = text.Substring(start, length);
        return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    private static int FindNextSeparator(string message, int start, int endExclusive)
    {
        for (var i = start; i < endExclusive; i++)
        {
            if (IsTokenSeparator(message[i]))
                return i;
        }

        return -1;
    }

    private bool CanAppendAsPlainText(LogRenderContext context)
        => !_tokenResolution.HasResolvers && !PayloadFormattingHelpers.HasStructuredPayloadTypeMetadata(context);

    private static bool TryAppendObjectWhitespace(string message, RenderTokenWriter writer, ref int index)
    {
        if (!char.IsWhiteSpace(message[index]))
            return false;

        var wsStart = index++;
        while (index < message.Length && char.IsWhiteSpace(message[index]))
            index++;
        writer.Add(message, wsStart, index - wsStart, LogSemanticStyle.SecondaryText);
        return true;
    }

    private static bool TryAppendObjectPunctuation(string message, RenderTokenWriter writer, ref int index, ref bool expectingValue)
    {
        var ch = message[index];
        if (ch is not ('{' or '}' or '[' or ']' or ','))
            return false;

        writer.Add(message, index, 1, LogSemanticStyle.Punctuation);
        if (ch == ',')
            expectingValue = false;
        index++;
        return true;
    }

    private static bool TryAppendAssignment(string message, RenderTokenWriter writer, ref int index, ref bool expectingValue)
    {
        if (message[index] != '=')
            return false;

        writer.Add(message, index, 1, LogSemanticStyle.Punctuation);
        expectingValue = true;
        index++;
        return true;
    }

    private static int ReadObjectTokenEnd(string message, int start)
    {
        var index = start;
        while (index < message.Length &&
               !char.IsWhiteSpace(message[index]) &&
               message[index] is not '{' and not '}' and not '[' and not ']' and not ',' and not '=')
        {
            index++;
        }

        return index;
    }

    private static List<(int Start, int Length)> CollectStructuredTypeNameSpans(LogRenderContext context, string message)
    {
        var spans = new List<(int Start, int Length)>(4);
        var typeNames = PayloadFormattingHelpers.GetStructuredTypeNames(context.Properties);
        for (var i = 0; i < typeNames.Count; i++)
        {
            var typeName = typeNames[i];
            if (string.IsNullOrWhiteSpace(typeName))
                continue;

            var searchIndex = 0;
            while (searchIndex < message.Length)
            {
                var foundIndex = message.IndexOf(typeName, searchIndex, StringComparison.Ordinal);
                if (foundIndex < 0)
                    break;

                spans.Add((foundIndex, typeName.Length));
                searchIndex = foundIndex + typeName.Length;
            }
        }

        return spans;
    }

    private void AppendStructuredTypeSpans(
        LogRenderContext context,
        string message,
        IReadOnlyList<(int Start, int Length)> spans,
        RenderTokenWriter writer)
    {
        var cursor = 0;
        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            if (span.Start < cursor)
                continue;

            if (span.Start > cursor)
                AppendNonUrlTokenAwareSegments(context, message, cursor, span.Start - cursor, writer);

            writer.Add(message, span.Start, span.Length, LogSemanticStyle.JsonString);
            cursor = span.Start + span.Length;
        }

        if (cursor < message.Length)
            AppendNonUrlTokenAwareSegments(context, message, cursor, message.Length - cursor, writer);
    }

    private static bool LooksLikeObjectLiteral(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed.Length < 4)
            return false;

        if (trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
            return false;

        if (trimmed.Contains(":"))
            return false;

        return trimmed.Contains("=");
    }

    private static bool NextNonWhitespaceEquals(string text, int start, char expected)
    {
        for (var i = start; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i]))
                continue;
            return text[i] == expected;
        }

        return false;
    }

    private static bool IsTokenSeparator(char ch)
    {
        return ch is ' ' or '\t' or '\r' or '\n' or ',' or ';' or ':' or '=' or '(' or ')' or '[' or ']' or '{' or '}' or '<' or '>' or '"' or '\'' or '|' or '/' or '\\';
    }

}
