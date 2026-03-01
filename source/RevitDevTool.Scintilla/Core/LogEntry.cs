using Microsoft.Extensions.Logging;
namespace RevitDevTool.Scintilla.Core;

public sealed class LogEntry
{
    public static readonly IReadOnlyDictionary<string, object?> EmptyProperties = new Dictionary<string, object?>();

    private Action<byte[]>? _bufferReleaser;
    private int _bufferReleased;
    private string? _messageTextCache;
    private string? _messageTextUpperInvariantCache;
    private string? _metadataSearchTextCache;
    private string? _metadataSearchTextUpperInvariantCache;

    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public LogLevel Level { get; init; } = LogLevel.Information;
    public ArraySegment<byte> Message { get; init; }
    public string Source { get; init; } = string.Empty;
    public string? ExceptionText { get; init; }
    public IReadOnlyDictionary<string, object?> Properties { get; init; } = EmptyProperties;

    internal string GetOrCreateMessageText()
    {
        var cached = _messageTextCache;
        if (cached is not null)
            return cached;

        if (Message.Array is null || Message.Count <= 0)
            return string.Empty;

        var decoded = System.Text.Encoding.UTF8.GetString(Message.Array, Message.Offset, Message.Count);
        Interlocked.CompareExchange(ref _messageTextCache, decoded, null);
        return _messageTextCache ?? decoded;
    }

    internal string GetOrCreateMetadataSearchText()
    {
        var cached = _metadataSearchTextCache;
        if (cached is not null)
            return cached;

        var builder = new System.Text.StringBuilder(128);

        if (!string.IsNullOrEmpty(Source))
            builder.Append(Source).Append('\n');

        if (!string.IsNullOrEmpty(ExceptionText))
            builder.Append(ExceptionText).Append('\n');

        foreach (var pair in Properties)
        {
            if (!string.IsNullOrEmpty(pair.Key))
                builder.Append(pair.Key).Append('=');

            if (pair.Value is string stringValue)
            {
                builder.Append(stringValue);
            }
            else if (pair.Value is not null)
            {
                builder.Append(pair.Value);
            }

            builder.Append('\n');
        }

        var materialized = builder.Length == 0 ? string.Empty : builder.ToString();
        Interlocked.CompareExchange(ref _metadataSearchTextCache, materialized, null);
        return _metadataSearchTextCache ?? materialized;
    }

    internal string GetOrCreateMessageTextUpperInvariant()
    {
        var cached = _messageTextUpperInvariantCache;
        if (cached is not null)
            return cached;

        var upper = GetOrCreateMessageText().ToUpperInvariant();
        Interlocked.CompareExchange(ref _messageTextUpperInvariantCache, upper, null);
        return _messageTextUpperInvariantCache ?? upper;
    }

    internal string GetOrCreateMetadataSearchTextUpperInvariant()
    {
        var cached = _metadataSearchTextUpperInvariantCache;
        if (cached is not null)
            return cached;

        var upper = GetOrCreateMetadataSearchText().ToUpperInvariant();
        Interlocked.CompareExchange(ref _metadataSearchTextUpperInvariantCache, upper, null);
        return _metadataSearchTextUpperInvariantCache ?? upper;
    }

    public void AttachBufferLease(Action<byte[]> releaser)
    {
        _bufferReleaser = releaser ?? throw new ArgumentNullException(nameof(releaser));
        _bufferReleased = 0;
    }

    public void ReleaseBuffer()
    {
        var releaser = _bufferReleaser;
        if (releaser is null)
            return;

        if (Interlocked.Exchange(ref _bufferReleased, 1) != 0)
            return;

        var buffer = Message.Array;
        if (buffer is null)
            return;

        releaser(buffer);
    }
}
