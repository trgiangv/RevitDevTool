using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Core;
namespace RevitDevTool.Scintilla.Render;

public sealed class DefaultLogStyleRegistry : ILogStyleRegistry
{
    public static DefaultLogStyleRegistry Instance { get; } = new();

#if NET8_0_OR_GREATER
    private readonly FrozenDictionary<string, int> _customStyleIds;
#else
    private readonly IReadOnlyDictionary<string, int> _customStyleIds;
#endif

    public DefaultLogStyleRegistry()
    {
#if NET8_0_OR_GREATER
        // FrozenDictionary: O(1) lookup with ~50% lower lookup cost than Dictionary for
        // small sets. Built once at startup and never mutated — ideal for hot-path style resolution.
        _customStyleIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [StyleToken.TokenLink]       = TokenLinkStyleId,
            [StyleToken.TokenEmphasis]   = TokenEmphasisStyleId,
            [StyleToken.TokenClassified] = TokenClassifiedStyleId
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
#else
        _customStyleIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [StyleToken.TokenLink]       = TokenLinkStyleId,
            [StyleToken.TokenEmphasis]   = TokenEmphasisStyleId,
            [StyleToken.TokenClassified] = TokenClassifiedStyleId
        };
#endif
    }

    public int TraceStyleId { get; init; } = 10;
    public int DebugStyleId { get; init; } = 11;
    public int InformationStyleId { get; init; } = 12;
    public int WarningStyleId { get; init; } = 13;
    public int ErrorStyleId { get; init; } = 14;
    public int CriticalStyleId { get; init; } = 15;

    public int TokenIndicatorStyleId { get; init; } = 8;
    public Color TokenIndicatorColor { get; init; } = Color.FromArgb(80, 140, 220);
    public bool TokenIndicatorUnderText { get; init; } = true;
    public int LinkHotspotStyleId { get; init; } = 16;
    public int SecondaryTextStyleId { get; init; } = 17;
    public int PunctuationStyleId { get; init; } = 18;
    public int JsonKeyStyleId { get; init; } = 19;
    public int JsonStringStyleId { get; init; } = 20;
    public int JsonNumberStyleId { get; init; } = 21;
    public int JsonBooleanStyleId { get; init; } = 22;
    public int JsonNullStyleId { get; init; } = 23;
    public int TokenLinkStyleId { get; init; } = 24;
    public int TokenEmphasisStyleId { get; init; } = 25;
    public int InvalidTokenStyleId { get; init; } = 26;
    public int TextStyleId { get; init; } = 27;
    public int TokenClassifiedStyleId { get; init; } = 28;

    public int GetStyleId(LogLevel level) => level switch
    {
        LogLevel.Trace => TraceStyleId,
        LogLevel.Debug => DebugStyleId,
        LogLevel.Information => InformationStyleId,
        LogLevel.Warning => WarningStyleId,
        LogLevel.Error => ErrorStyleId,
        LogLevel.Critical => CriticalStyleId,
        _ => InformationStyleId
    };

    public int GetStyleId(LogSemanticStyle semanticStyle) => semanticStyle switch
    {
        LogSemanticStyle.Text => TextStyleId,
        LogSemanticStyle.SecondaryText => SecondaryTextStyleId,
        LogSemanticStyle.Punctuation => PunctuationStyleId,
        LogSemanticStyle.LevelTrace => TraceStyleId,
        LogSemanticStyle.LevelDebug => DebugStyleId,
        LogSemanticStyle.LevelInformation => InformationStyleId,
        LogSemanticStyle.LevelWarning => WarningStyleId,
        LogSemanticStyle.LevelError => ErrorStyleId,
        LogSemanticStyle.LevelCritical => CriticalStyleId,
        LogSemanticStyle.JsonKey => JsonKeyStyleId,
        LogSemanticStyle.JsonString => JsonStringStyleId,
        LogSemanticStyle.JsonNumber => JsonNumberStyleId,
        LogSemanticStyle.JsonBoolean => JsonBooleanStyleId,
        LogSemanticStyle.JsonNull => JsonNullStyleId,
        LogSemanticStyle.TokenLink => TokenLinkStyleId,
        LogSemanticStyle.TokenEmphasis => TokenEmphasisStyleId,
        LogSemanticStyle.InvalidToken => InvalidTokenStyleId,
        LogSemanticStyle.TokenClassified => TokenClassifiedStyleId,
        _ => TextStyleId
    };

    public int GetTokenIndicatorStyleId() => TokenIndicatorStyleId;
    public int GetLinkHotspotStyleId() => LinkHotspotStyleId;
    public bool TryGetStyleId(string styleKey, out int styleId) => _customStyleIds.TryGetValue(styleKey, out styleId);
    public IReadOnlyDictionary<string, int> CustomStyleIds => _customStyleIds;
}
