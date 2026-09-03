namespace DevTools.Daemon.Desktop;

public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;

    public bool IsFirstInstance { get; }

    public SingleInstance()
    {
        _mutex = new Mutex(true, AppConstants.MutexName, out var createdNew);
        IsFirstInstance = createdNew;
    }

    public void Dispose()
    {
        if (IsFirstInstance)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
