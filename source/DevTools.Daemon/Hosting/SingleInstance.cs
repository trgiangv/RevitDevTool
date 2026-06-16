namespace DevTools.Daemon.Hosting;

/// <summary>
/// Global mutex ensuring only one daemon instance runs.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = "DevToolsDaemon_v1";
    private readonly Mutex _mutex;

    public bool IsFirstInstance { get; }

    public SingleInstance()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;
    }

    public void Dispose()
    {
        if (IsFirstInstance)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
