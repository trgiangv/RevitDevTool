using System.Buffers;
using System.Collections;
using System.IO;
using System.Text;
using System.Text.Json;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Helpers;
using RevitDevTool.Scintilla.Internal;
namespace RevitDevTool.Scintilla.Formatting;

internal sealed class JsonValueFormatter
{
    private readonly bool _enablePrettyJson;
    private readonly ILogEnrichmentCallbacks? _callbacks;
    private readonly Action<Exception>? _errorSink;
    private readonly UrlSegmentHelper _urlHelper = new();

    public JsonValueFormatter(bool enablePrettyJson, ILogEnrichmentCallbacks? callbacks, Action<Exception>? errorSink)
    {
        _enablePrettyJson = enablePrettyJson;
        _callbacks = callbacks;
        _errorSink = errorSink;
    }

    public bool TryAppendPrettyJsonSegments(LogRenderContext context, string message, IList<RenderSegment> segments)
    {
        if (!TryGetPrettyPrintedMessage(context, message, out var pretty))
            return false;

        var writer = new RenderTokenWriter(segments);
        AppendJsonStyledSegments(pretty, writer);
        return true;
    }

    public bool TryAppendSegments(LogRenderContext context, string message, IList<RenderSegment> segments)
        => TryAppendPrettyJsonSegments(context, message, segments);

    public bool TryGetPrettyPrintedMessage(LogRenderContext context, string message, out string pretty)
    {
        if (!_enablePrettyJson)
        {
            pretty = string.Empty;
            return false;
        }

        if (!CanPrettyPrint(context))
        {
            pretty = string.Empty;
            return false;
        }

        var hasStructuredPayload = TryResolveStructuredPayloadUtf8(context, out var utf8Json);

        if (hasStructuredPayload && TryInlineStructuredPayloadBlocks(context, message, out var inlined))
        {
            pretty = inlined;
            return true;
        }

        if (!hasStructuredPayload)
        {
            if (!LooksLikeJson(message))
            {
                pretty = string.Empty;
                return false;
            }

            utf8Json = context.MessageBytes.Length > 0
                ? context.MessageBytes
                : Encoding.UTF8.GetBytes(message);
        }

        if (!TryPrettyPrintUtf8(utf8Json.Span, out pretty))
            return false;

        if (hasStructuredPayload && ShouldPreserveOriginalTextPrefix(context, message))
            pretty = message + Environment.NewLine + pretty;

        return true;
    }

    private bool CanPrettyPrint(LogRenderContext context)
    {
        if (_callbacks is null)
            return true;

        try
        {
            return _callbacks.ShouldPrettyPrint(context);
        }
        catch (Exception ex)
        {
            _errorSink?.Invoke(ex);
            return false;
        }
    }

    private bool TryResolveStructuredPayloadUtf8(LogRenderContext context, out ReadOnlyMemory<byte> utf8Json)
    {
        if (TryGetImplicitStructuredPayloadUtf8(context, out utf8Json))
            return true;

        try
        {
            return _callbacks?.TryGetStructuredPayload(context, out utf8Json) ?? false;
        }
        catch (Exception ex)
        {
            _errorSink?.Invoke(ex);
            utf8Json = default;
            return false;
        }
    }

    private bool TryGetImplicitStructuredPayloadUtf8(LogRenderContext context, out ReadOnlyMemory<byte> utf8Json)
    {
        utf8Json = default;
        if (!context.Properties.TryGetValue(LogPropertyKeys.StructuredPayloadObject, out var payload) || payload is null)
            return false;

        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
            if (bytes.Length == 0)
                return false;

            utf8Json = bytes;
            return true;
        }
        catch (Exception ex)
        {
            _errorSink?.Invoke(ex);
            return false;
        }
    }

    private void AppendJsonStyledSegments(string text, RenderTokenWriter writer)
    {
        var index = 0;
        while (index < text.Length)
        {
            if (TryAppendWhitespace(text, writer, ref index) ||
                TryAppendPunctuation(text, writer, ref index) ||
                TryAppendQuotedString(text, writer, ref index) ||
                TryAppendNumber(text, writer, ref index) ||
                TryAppendKeyword(text, writer, ref index))
            {
                continue;
            }

            writer.Add(text, index, 1, LogSemanticStyle.Text);
            index++;
        }
    }

    private static bool TryAppendWhitespace(string text, RenderTokenWriter writer, ref int index)
    {
        if (!char.IsWhiteSpace(text[index]))
            return false;

        var start = index;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;

        writer.Add(text, start, index - start, LogSemanticStyle.SecondaryText);
        return true;
    }

    private static bool TryAppendPunctuation(string text, RenderTokenWriter writer, ref int index)
    {
        if (text[index] is not ('{' or '}' or '[' or ']' or ',' or ':'))
            return false;

        writer.Add(text, index, 1, LogSemanticStyle.Punctuation);
        index++;
        return true;
    }

    private bool TryAppendQuotedString(string text, RenderTokenWriter writer, ref int index)
    {
        if (text[index] != '"')
            return false;

        var start = index;
        index = FindQuotedStringEnd(text, index);
        var tokenLength = index - start;
        var style = IsJsonKeyString(text, index) ? LogSemanticStyle.JsonKey : LogSemanticStyle.JsonString;

        if (style == LogSemanticStyle.JsonString)
            AppendJsonStringWithLinks(text, start, tokenLength, writer);
        else
            writer.Add(text, start, tokenLength, style);

        return true;
    }

    private static bool TryAppendNumber(string text, RenderTokenWriter writer, ref int index)
    {
        var ch = text[index];
        if (!char.IsDigit(ch) && ch != '-')
            return false;

        var start = index++;
        while (index < text.Length && (char.IsDigit(text[index]) || text[index] is '.' or 'e' or 'E' or '+' or '-'))
            index++;

        writer.Add(text, start, index - start, LogSemanticStyle.JsonNumber);
        return true;
    }

    private static bool TryAppendKeyword(string text, RenderTokenWriter writer, ref int index)
    {
        if (MatchesWordAt(text, index, "true") || MatchesWordAt(text, index, "false"))
        {
            var length = text[index] == 't' ? 4 : 5;
            writer.Add(text, index, length, LogSemanticStyle.JsonBoolean);
            index += length;
            return true;
        }

        if (MatchesWordAt(text, index, "null"))
        {
            writer.Add("null", LogSemanticStyle.JsonNull);
            index += 4;
            return true;
        }

        return false;
    }

    private static int FindQuotedStringEnd(string text, int startIndex)
    {
        var index = startIndex + 1;
        var escaped = false;
        while (index < text.Length)
        {
            var current = text[index++];
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                continue;
            }

            if (current == '"')
                break;
        }

        return index;
    }

    private static bool IsJsonKeyString(string text, int tokenEnd)
    {
        var lookAhead = tokenEnd;
        while (lookAhead < text.Length && char.IsWhiteSpace(text[lookAhead]))
            lookAhead++;

        return lookAhead < text.Length && text[lookAhead] == ':';
    }

    private static bool MatchesWordAt(string text, int start, string word)
    {
        if (start < 0 || start + word.Length > text.Length)
            return false;

        for (var i = 0; i < word.Length; i++)
        {
            if (text[start + i] != word[i])
                return false;
        }

        return true;
    }

    private void AppendJsonStringWithLinks(string source, int start, int length, RenderTokenWriter writer)
    {
        if (length <= 2)
        {
            writer.Add(source, start, length, LogSemanticStyle.JsonString);
            return;
        }

        var content = source.Substring(start + 1, length - 2);

        if (!Helpers.UrlScanner.HasPotentialCandidate(content))
        {
            writer.Add(source, start, length, LogSemanticStyle.JsonString);
            return;
        }

        writer.Add("\"", LogSemanticStyle.JsonString);
        _urlHelper.AppendUrlAwareContent(content, LogSemanticStyle.JsonString, writer);
        writer.Add("\"", LogSemanticStyle.JsonString);
    }

    private static bool TryPrettyPrintUtf8(ReadOnlySpan<byte> utf8Json, out string pretty)
    {
        try
        {
#if NET8_0_OR_GREATER
            var jsonBytes = utf8Json.ToArray();
            using var document = JsonDocument.Parse(jsonBytes);
            var writerBuffer = new ArrayBufferWriter<byte>(utf8Json.Length + 256);
            using (var writer = new Utf8JsonWriter(writerBuffer, new JsonWriterOptions { Indented = true }))
            {
                document.WriteTo(writer);
            }

            pretty = Encoding.UTF8.GetString(writerBuffer.WrittenSpan);
            return true;
#else
            var jsonBytes = utf8Json.ToArray();
            using var document = JsonDocument.Parse(jsonBytes);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                document.WriteTo(writer);
            }

            pretty = Encoding.UTF8.GetString(stream.ToArray());
            return true;
#endif
        }
        catch
        {
            pretty = string.Empty;
            return false;
        }
    }

    private static bool LooksLikeJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.TrimStart();
        if (trimmed.Length < 2)
            return false;

        return (trimmed[0] == '{' && trimmed.Contains('}')) ||
               (trimmed[0] == '[' && trimmed.Contains(']'));
    }

    private static bool ShouldPreserveOriginalTextPrefix(LogRenderContext context, string message)
    {
        if (string.IsNullOrWhiteSpace(message) || LooksLikeJson(message))
            return false;

        var typeNames = PayloadFormattingHelpers.GetStructuredTypeNames(context.Properties);
        if (typeNames.Count == 0)
            return false;

        if (typeNames.Count > 1)
            return message.Contains("=", StringComparison.Ordinal) || message.Contains(";", StringComparison.Ordinal) || message.Contains(":", StringComparison.Ordinal);

        var typeName = typeNames[0];
        var trimmed = message.Trim();
        return !string.Equals(trimmed, typeName, StringComparison.Ordinal) && message.Contains(typeName, StringComparison.Ordinal);
    }

    private static bool TryInlineStructuredPayloadBlocks(LogRenderContext context, string message, out string inlined)
    {
        inlined = string.Empty;
        if (string.IsNullOrWhiteSpace(message) || LooksLikeJson(message))
            return false;

        if (!context.Properties.TryGetValue(LogPropertyKeys.StructuredPayloadObject, out var payloadObj) || payloadObj is null)
            return false;

        var typeNames = PayloadFormattingHelpers.GetStructuredTypeNames(context.Properties);
        if (typeNames.Count == 0)
            return false;

        if (payloadObj is not IEnumerable payloadEnumerable || payloadObj is string)
            return false;

        var payloadItems = new List<object>(4);
        foreach (var item in payloadEnumerable)
        {
            if (item is not null)
                payloadItems.Add(item);
        }

        if (payloadItems.Count <= 1 || typeNames.Count <= 1)
            return false;

        var rewritten = message;
        var replacements = Math.Min(payloadItems.Count, typeNames.Count);
        for (var i = 0; i < replacements; i++)
        {
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payloadItems[i]);
            if (!TryPrettyPrintUtf8(jsonBytes, out var prettyObject))
                return false;

            var block = BuildInlinePrettyBlock(prettyObject);
            rewritten = ReplaceFirst(rewritten, typeNames[i], block);
        }

        inlined = rewritten;
        return true;
    }

    private static string BuildInlinePrettyBlock(string prettyObject)
    {
        using var reader = new StringReader(prettyObject);
        using var writer = new StringWriter();
        string? line;
        var first = true;
        while ((line = reader.ReadLine()) is not null)
        {
            if (first)
            {
                writer.WriteLine();
                first = false;
            }

            writer.Write("  ");
            writer.WriteLine(line);
        }

        writer.Write("  ");
        return writer.ToString();
    }

    private static string ReplaceFirst(string source, string search, string replacement)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(search))
            return source;

        var index = source.IndexOf(search, StringComparison.Ordinal);
        if (index < 0)
            return source;

        return source.Substring(0, index) + replacement + source.Substring(index + search.Length);
    }

}
