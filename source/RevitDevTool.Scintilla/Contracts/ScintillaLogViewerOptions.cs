using System.Threading.Channels;

namespace RevitDevTool.Scintilla.Contracts;

public sealed class ScintillaLogViewerOptions
{
    public int ChannelCapacity { get; init; } = 20_000;
    public int FlushIntervalMs { get; init; } = 75;
    public int MaxLines { get; init; } = 50_000;
    // Keep history bounded and independent from MaxLines to avoid hidden memory duplication.
    public int MaxHistoryEntries { get; init; } = 50_000;
    public int TrimChunkLines { get; init; } = 1_000;
    public int MaxBatchSize { get; init; } = 500;
    public bool AutoScroll { get; init; } = true;
    public BoundedChannelFullMode DropPolicy { get; init; } = BoundedChannelFullMode.DropOldest;
}
