using System.Threading.Channels;
using RevitDevTool.Scintilla.Formatting;
using RevitDevTool.Scintilla.Render;
namespace RevitDevTool.Scintilla.Core;

public sealed class ScintillaLogViewerOptions
{
    // ── Channel / Ingest ──────────────────────────────────────────────
    public int ChannelCapacity { get; init; } = 20_000;
    public BoundedChannelFullMode DropPolicy { get; init; } = BoundedChannelFullMode.DropOldest;
    public int MaxBatchSize { get; init; } = 500;
    public int FlushIntervalMs { get; init; } = 75;

    // ── Display / Document ────────────────────────────────────────────
    public int MaxLines { get; init; } = 50_000;
    public int TrimChunkLines { get; init; } = 1_000;
    public bool AutoScroll { get; init; } = true;
    public string FontFamily { get; init; } = "Cascadia Mono";
    public int FontSize { get; init; } = 10;

    // ── History ───────────────────────────────────────────────────────
    public int MaxHistoryEntries { get; init; } = 50_000;
    public bool DisableHistory { get; init; }

    // ── Theme / Styling ───────────────────────────────────────────────
    public ScintillaTheme Theme { get; init; } = ScintillaTheme.Dark;
    public ILogThemeProvider? ThemeProvider { get; init; }
    public ILogStyleRegistry? StyleRegistry { get; init; }

    // ── Token / Enrichment ────────────────────────────────────────────
    public ILogTokenClassifier? TokenClassifier { get; init; }
    public ILogEnrichmentCallbacks? EnrichmentCallbacks { get; init; }
    public Action<Exception>? EnrichmentErrorSink { get; init; }
    public bool EnableTokenLinks { get; init; } = true;
    public bool EnableTokenHighlight { get; init; } = true;
    public int LinkStyleOffset { get; init; } = 100;
    public bool EnableSingleClickLinkActivation { get; init; } = true;
    public bool RequireCtrlForLinkActivation { get; init; }
    public Action<ILogTokenPayload>? TokenLinkClicked { get; init; }

    // ── JSON ──────────────────────────────────────────────────────────
    public bool EnablePrettyJson { get; init; }
}
