namespace DevTools.Execution.Diagnostics;

/// <summary>
/// One debugger listen slot (CPython or IronPython). Reserve on the CAD
/// start thread so the UI chip has a port; listen later after the engine
/// is up. Attach is polled off the UI thread.
/// </summary>
public sealed class DebugEndpoint : IDisposable
{
    private static readonly TimeSpan AttachPoll = TimeSpan.FromSeconds(2);

    private readonly Lock _gate = new();
    private DebugPortLease? _lease;
    private Timer? _attachTimer;
    private Func<bool>? _attachProbe;

    public int Port { get; private set; }
    public bool Attached { get; private set; }
    public bool IsListening { get; private set; }

    public Func<bool>? AttachProbe
    {
        get
        {
            lock (_gate)
                return _attachProbe;
        }
        set
        {
            lock (_gate)
                _attachProbe = value;
        }
    }

    public void Reserve(int preferredPort)
    {
        lock (_gate)
        {
            if (_lease is not null || Port != 0)
                return;

            _lease = DebugPortLease.Acquire(preferredPort);
            Port = _lease.Port;
            Attached = false;
        }
    }

    /// <summary>
    /// Drop the placeholder socket (and mutex) so the debugger runtime can
    /// bind. <see cref="Port"/> stays until <see cref="MarkListening"/>.
    /// </summary>
    public int ReleaseLease()
    {
        lock (_gate)
        {
            if (_lease is not null)
            {
                var port = _lease.Port;
                _lease.Dispose();
                _lease = null;
                Port = port;
                return port;
            }

            if (Port == 0)
                throw new InvalidOperationException("Debug port was not reserved.");

            return Port;
        }
    }

    public void MarkListening(int port)
    {
        lock (_gate)
        {
            Port = port;
            IsListening = true;
            _attachTimer ??= new Timer(static s => ((DebugEndpoint)s!).PollAttach(), this, AttachPoll, AttachPoll);
        }
    }

    public void MarkFailed()
    {
        lock (_gate)
        {
            DropLease();
            StopTimer();
            Attached = false;
            IsListening = false;
            Port = 0;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            DropLease();
            StopTimer();
            _attachProbe = null;
            Port = 0;
            Attached = false;
            IsListening = false;
        }
    }

    public void Dispose() => Reset();

    private void PollAttach()
    {
        Func<bool>? probe;
        lock (_gate)
            probe = _attachProbe;

        var attached = false;
        if (probe is not null)
        {
            try
            {
                attached = probe();
            }
            catch
            {
                attached = false;
            }
        }

        lock (_gate)
            Attached = attached;
    }

    private void DropLease()
    {
        _lease?.Dispose();
        _lease = null;
    }

    private void StopTimer()
    {
        _attachTimer?.Dispose();
        _attachTimer = null;
    }
}

public sealed class DebugEndpoints : IDisposable
{
    public DebugEndpoint CPython { get; } = new();
    public DebugEndpoint IronPython { get; } = new();

    public void Dispose()
    {
        CPython.Dispose();
        IronPython.Dispose();
    }
}
