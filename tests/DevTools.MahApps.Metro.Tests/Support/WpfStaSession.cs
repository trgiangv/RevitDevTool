using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace DevTools.MetroFork.Tests.Support;

public sealed class WpfStaSession : IDisposable
{
    private readonly Thread _uiThread;
    private readonly ManualResetEventSlim _ready = new(false);
    private Exception? _startupError;

    public WpfStaSession()
    {
        _uiThread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "WpfStaSession",
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
        _ready.Wait(TimeSpan.FromSeconds(30));
        if (_startupError is not null)
            ExceptionDispatchInfo.Capture(_startupError).Throw();
    }

    public Dispatcher Dispatcher { get; private set; } = null!;

    public void Invoke(Action action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Exception? captured = null;
        Send(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        if (captured is not null)
            ExceptionDispatchInfo.Capture(captured).Throw();
    }

    public T Invoke<T>(Func<T> fn)
    {
        T? result = default;
        Invoke(() => { result = fn(); });
        return result!;
    }

    private void RunMessageLoop()
    {
        try
        {
            if (Application.Current is null)
                _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            Dispatcher = Dispatcher.CurrentDispatcher;
            _ready.Set();
            Dispatcher.Run();
        }
        catch (Exception ex)
        {
            _startupError = ex;
            _ready.Set();
        }
    }

    private void Send(Action action)
    {
        if (Dispatcher.CheckAccess())
            action();
        else
            Dispatcher.Invoke(action);
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        Send(() => Dispatcher.InvokeShutdown());
        _uiThread.Join(TimeSpan.FromSeconds(10));
        _ready.Dispose();
    }
}
