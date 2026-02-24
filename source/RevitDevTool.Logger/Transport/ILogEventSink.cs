namespace RevitDevTool.Logger.Transport;

public interface ILogEventSink
{
    ValueTask PublishAsync(LogEventData logEvent, CancellationToken cancellationToken = default);
}
