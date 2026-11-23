namespace DevTools.ModelsContext.Panel;

public interface IClock
{
    DateTimeOffset Now { get; }
}

public interface IScheduler
{
    SynchronizationContext Context { get; }

    IDisposable Schedule(TimeSpan delay, Action action);
}
