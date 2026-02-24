namespace RevitDevTool.Logger.Transport;

/// <summary>
/// Simple async sink adapter around a callback publisher.
/// </summary>
public sealed class PipeLogSink : ILogEventSink
{
    private readonly Func<LogEventData, CancellationToken, ValueTask> _publisher;

    public PipeLogSink(Func<LogEventData, CancellationToken, ValueTask> publisher)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public ValueTask PublishAsync(LogEventData logEvent, CancellationToken cancellationToken = default)
    {
        return _publisher(logEvent, cancellationToken);
    }
}
