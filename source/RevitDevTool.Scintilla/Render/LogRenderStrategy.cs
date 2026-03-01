using System.Text;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Formatting;
namespace RevitDevTool.Scintilla.Render;

public sealed class LogRenderStrategy : ISegmentLogRenderStrategy
{
    private readonly string _fontFamily;
    private readonly int _fontSize;
    private readonly ILogThemeProvider _themeProvider;
    private readonly ILogStyleRegistry _styleRegistry;
    private readonly bool _enablePrettyJson;
    private readonly bool _enableTokenProcessing;
    private readonly RenderOrchestrator _renderOrchestrator;

    public LogRenderStrategy()
        : this("Cascadia Mono", 10, new StaticLogThemeProvider(ScintillaTheme.Dark), DefaultLogStyleRegistry.Instance, null)
    {
    }

    public LogRenderStrategy(string fontFamily, int fontSize, ScintillaTheme theme)
        : this(fontFamily, fontSize, new StaticLogThemeProvider(theme), DefaultLogStyleRegistry.Instance, null)
    {
    }

    public LogRenderStrategy(
        string fontFamily,
        int fontSize,
        ILogThemeProvider? themeProvider,
        ILogStyleRegistry? styleRegistry = null,
        ScintillaLogViewerOptions? options = null)
    {
        _fontFamily = string.IsNullOrWhiteSpace(fontFamily) ? "Cascadia Mono" : fontFamily;
        _fontSize = fontSize <= 0 ? 10 : fontSize;
        _themeProvider = themeProvider ?? new StaticLogThemeProvider(ScintillaTheme.Dark);
        _styleRegistry = styleRegistry ?? DefaultLogStyleRegistry.Instance;

        var resolvedOptions = options ?? new ScintillaLogViewerOptions();
        _enablePrettyJson = resolvedOptions.EnablePrettyJson;
        _enableTokenProcessing = resolvedOptions.EnrichmentCallbacks is not null ||
                                 resolvedOptions.TokenClassifier is not null;

        var tokenResolution = new TokenResolutionEngine(
            resolvedOptions.EnrichmentCallbacks,
            resolvedOptions.TokenClassifier,
            resolvedOptions.EnrichmentErrorSink);

        var displayFormatter = new DisplayValueFormatter(tokenResolution);

        var jsonFormatter = new JsonValueFormatter(
            _enablePrettyJson,
            resolvedOptions.EnrichmentCallbacks,
            resolvedOptions.EnrichmentErrorSink);

        _renderOrchestrator = new RenderOrchestrator(_enablePrettyJson, jsonFormatter, displayFormatter)
        {
            TokenResolution = tokenResolution
        };
    }

    public int GetStyleId(LogLevel level) => _styleRegistry.GetStyleId(level);

    public void ConfigureStyles(IStyleWriter styleWriter)
    {
        var theme = _themeProvider.CurrentTheme;
        styleWriter.SetDefaultStyle(_fontFamily, _fontSize, theme.Text, theme.Background);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogLevel.Trace), theme.Trace, theme.Background);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogLevel.Debug), theme.Debug, theme.Background);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogLevel.Information), theme.Information, theme.Background);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogLevel.Warning), theme.Warning, theme.Background, bold: true);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogLevel.Error), theme.Error, theme.Background, bold: true);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogLevel.Critical), theme.Critical, theme.Background, bold: true);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogSemanticStyle.Text), theme.Text, theme.Background);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogSemanticStyle.SecondaryText), theme.SecondaryText, theme.Background);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogSemanticStyle.Punctuation), theme.TertiaryText, theme.Background);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogSemanticStyle.JsonKey), theme.Name, theme.Background, bold: true);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogSemanticStyle.JsonString), theme.String, theme.Background);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogSemanticStyle.JsonNumber), theme.Number, theme.Background);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogSemanticStyle.JsonBoolean), theme.Boolean, theme.Background, bold: true);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogSemanticStyle.JsonNull), theme.Null, theme.Background, bold: true);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogSemanticStyle.TokenLink), theme.TokenLink, theme.Background, bold: true);
        styleWriter.SetStyle(_styleRegistry.GetLinkHotspotStyleId(), theme.TokenLink, theme.Background, bold: true);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogSemanticStyle.TokenEmphasis), theme.TokenEmphasis, theme.Background, bold: true);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogSemanticStyle.TokenClassified), theme.TokenClassified, theme.Background, bold: true);
        styleWriter.SetStyle(_styleRegistry.GetStyleId(LogSemanticStyle.InvalidToken), theme.Invalid, theme.Background);

        foreach (var customStyle in _styleRegistry.CustomStyleIds)
        {
            if (theme.CustomStyles.TryGetValue(customStyle.Key, out var style))
                styleWriter.SetStyle(customStyle.Value, style.Foreground, style.Background, style.Bold);
        }
    }

    public void BuildSegments(LogEntry entry, IList<RenderSegment> segments)
    {
        segments.Clear();
        if (entry.Message.Array is null || entry.Message.Count <= 0)
            return;

        var utf8Slice = new ReadOnlyMemory<byte>(entry.Message.Array, entry.Message.Offset, entry.Message.Count);
        var hasAdvancedStyling = _enablePrettyJson || _enableTokenProcessing || entry.Properties.Count > 0;

        if (!hasAdvancedStyling)
        {
            var prefixResult = LogPrefixParser.TryParse(utf8Slice.Span);
            if (prefixResult.Found)
            {
                LogPrefixParser.BuildPrefixSegments(prefixResult, utf8Slice.Span, entry.Level, segments);
                return;
            }

            segments.Add(new RenderSegment(entry.Message.Count, LogSemanticStyle.Text));
            return;
        }

        var messageText = Encoding.UTF8.GetString(entry.Message.Array, entry.Message.Offset, entry.Message.Count);
        var stringPrefixResult = LogPrefixParser.TryParse(messageText, out var remainderMessage);
        var hasEmbeddedPrefix = stringPrefixResult.Found;
        var renderedMessage = hasEmbeddedPrefix ? remainderMessage : messageText;
        var renderedUtf8 = utf8Slice;

        if (hasEmbeddedPrefix)
        {
            LogPrefixParser.BuildPrefixSegments(stringPrefixResult, messageText, entry.Level, segments);

            var prefixCharCount = messageText.Length - remainderMessage.Length;
            if (prefixCharCount > 0)
            {
                var prefixBytes = stringPrefixResult.PrefixByteLength;
                if (prefixBytes > 0 && prefixBytes <= utf8Slice.Length)
                    renderedUtf8 = utf8Slice.Slice(prefixBytes);
            }
        }

        var renderContext = new LogRenderContext(
            entry.TimestampUtc,
            entry.Level,
            entry.Source,
            renderedMessage,
            renderedUtf8,
            entry.ExceptionText,
            entry.Properties);

        _renderOrchestrator.AppendMessageSegments(renderContext, renderedMessage, segments);
    }

    public bool TryFormatLine(LogEntry entry, out byte[] formattedLine)
    {
        formattedLine = Array.Empty<byte>();
        if (!_enablePrettyJson || entry.Message.Array is null || entry.Message.Count <= 0)
            return false;

        var utf8Slice = new ReadOnlyMemory<byte>(entry.Message.Array, entry.Message.Offset, entry.Message.Count);
        var prefixResult = LogPrefixParser.TryParse(utf8Slice.Span);

        ReadOnlyMemory<byte> renderedUtf8;
        string renderedMessage;

        if (prefixResult.Found)
        {
            renderedUtf8 = utf8Slice.Slice(prefixResult.PrefixByteLength);
            renderedMessage = renderedUtf8.Length > 0
                ? Encoding.UTF8.GetString(
                    entry.Message.Array,
                    entry.Message.Offset + prefixResult.PrefixByteLength,
                    entry.Message.Count - prefixResult.PrefixByteLength)
                : string.Empty;
        }
        else
        {
            renderedUtf8 = utf8Slice;
            renderedMessage = Encoding.UTF8.GetString(
                entry.Message.Array, entry.Message.Offset, entry.Message.Count);
        }

        var context = new LogRenderContext(
            entry.TimestampUtc,
            entry.Level,
            entry.Source,
            renderedMessage,
            renderedUtf8,
            entry.ExceptionText,
            entry.Properties);

        var prefixBytes = prefixResult.Found
            ? utf8Slice.Span.Slice(0, prefixResult.PrefixByteLength)
            : ReadOnlySpan<byte>.Empty;

        return _renderOrchestrator.TryFormatLine(entry, prefixResult.Found, prefixBytes, context, out formattedLine);
    }
}
