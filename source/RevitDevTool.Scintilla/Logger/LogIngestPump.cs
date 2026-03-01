using System.Threading.Channels;
using RevitDevTool.Scintilla.Core;
namespace RevitDevTool.Scintilla.Logger;

internal sealed class LogIngestPump
{
    private readonly ChannelWriter<LogEntry> _writer;
    private readonly ChannelReader<LogEntry> _reader;
    private readonly ScintillaLogViewerOptions _options;

    public LogIngestPump(ScintillaLogViewerOptions options)
    {
        _options = options;

        var channelOptions = new BoundedChannelOptions(Math.Max(1, options.ChannelCapacity))
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = options.DropPolicy,
            AllowSynchronousContinuations = true
        };

        var channel = Channel.CreateBounded<LogEntry>(channelOptions);
        _writer = channel.Writer;
        _reader = channel.Reader;
    }

    public IngestWriteResult TryPost(LogEntry entry)
    {
        if (_writer.TryWrite(entry))
            return IngestWriteResult.Accepted;

        return IngestWriteResult.Rejected;
    }

    public int TryReadBatch(List<LogEntry> targetBuffer)
    {
        targetBuffer.Clear();
        while (targetBuffer.Count < _options.MaxBatchSize && _reader.TryRead(out var entry))
            targetBuffer.Add(entry);

        return targetBuffer.Count;
    }

    public int Drain(Action<LogEntry> onEntry)
    {
        var count = 0;
        while (_reader.TryRead(out var entry))
        {
            onEntry(entry);
            count++;
        }

        return count;
    }

    public void Complete()
        => _writer.TryComplete();
}

internal enum IngestWriteResult
{
    Accepted,
    Rejected
}
