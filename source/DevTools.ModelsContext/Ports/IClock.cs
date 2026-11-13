namespace DevTools.ModelsContext.Ports;

public interface IClock
{
    DateTimeOffset Now { get; }
}

public interface IScheduler
{
    IDisposable Schedule(TimeSpan delay, Action action);
}
