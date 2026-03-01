using System.Text;
using RevitDevTool.Scintilla.Core;
namespace RevitDevTool.Scintilla.Formatting;

internal sealed class RenderOrchestrator
{
    private readonly bool _enablePrettyJson;
    private readonly JsonValueFormatter _jsonFormatter;
    private readonly DisplayValueFormatter _fallbackFormatter;

    public RenderOrchestrator(
        bool enablePrettyJson,
        JsonValueFormatter jsonFormatter,
        DisplayValueFormatter displayFormatter)
    {
        _enablePrettyJson = enablePrettyJson;
        _jsonFormatter = jsonFormatter;
        _fallbackFormatter = displayFormatter;
    }

    public TokenResolutionEngine TokenResolution { get; init; } = null!;

    /// <summary>
    /// Attempts to produce a pretty-printed (JSON-formatted) replacement for <paramref name="entry"/>.
    /// </summary>
    public bool TryFormatLine(
        LogEntry entry,
        bool hasEmbeddedPrefix,
        ReadOnlySpan<byte> prefixBytes,
        LogRenderContext context,
        out byte[] formattedLine)
    {
        formattedLine = Array.Empty<byte>();
        if (!_enablePrettyJson)
            return false;

        if (!_jsonFormatter.TryGetPrettyPrintedMessage(context, context.Message ?? string.Empty, out var prettyMessage))
            return false;

        var prettyBytes = Encoding.UTF8.GetBytes(prettyMessage);

        if (!hasEmbeddedPrefix || prefixBytes.IsEmpty)
        {
            formattedLine = prettyBytes;
            return true;
        }

        formattedLine = new byte[prefixBytes.Length + prettyBytes.Length];
        prefixBytes.CopyTo(formattedLine);
        prettyBytes.CopyTo(formattedLine.AsSpan(prefixBytes.Length));
        return true;
    }

    public void AppendMessageSegments(LogRenderContext context, string message, IList<RenderSegment> segments)
    {
        if (string.IsNullOrEmpty(message))
            return;

        if (_jsonFormatter.TryAppendSegments(context, message, segments))
            return;

        _fallbackFormatter.TryAppendSegments(context, message, segments);
    }
}
