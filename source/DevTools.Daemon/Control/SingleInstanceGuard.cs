namespace DevTools.Daemon.Control;

/// <summary>
///     Keeps one Daemon per user. A second start asks the first Daemon to show its window, then stops.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string ShowEventSuffix = "_Show";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _show;
    private RegisteredWaitHandle? _listener;

    public bool IsFirstInstance { get; }

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(true, AppConstants.MutexName, out var createdNew);
        IsFirstInstance = createdNew;
        if (IsFirstInstance) _show = new EventWaitHandle(false, EventResetMode.AutoReset, AppConstants.MutexName + ShowEventSuffix);
    }

    public void OnShowRequested(Action show) =>
        _listener = ThreadPool.RegisterWaitForSingleObject(_show!, (_, _) => show(), null, Timeout.Infinite, executeOnlyOnce: false);

    public static void RequestShow()
    {
        if (EventWaitHandle.TryOpenExisting(AppConstants.MutexName + ShowEventSuffix, out var show))
            using (show) show.Set();
    }

    public void Dispose()
    {
        _listener?.Unregister(null);
        _show?.Dispose();
        if (IsFirstInstance)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
