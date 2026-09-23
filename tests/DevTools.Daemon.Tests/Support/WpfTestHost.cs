using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace DevTools.Daemon.Tests.Support;

public sealed class WpfSession : IDisposable
{
    private readonly Thread _uiThread;
    private readonly ManualResetEventSlim _ready = new(false);
    private Exception? _startupError;

    public WpfSession()
    {
        _uiThread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "WpfSession",
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
        _ready.Wait(TimeSpan.FromSeconds(30));
        if (_startupError is not null)
            ExceptionDispatchInfo.Capture(_startupError).Throw();
        if (Application.Current is null)
            throw new InvalidOperationException("WPF application failed to start.");
    }

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

    public void InvokeAsync(Func<Task> action) =>
        Invoke(() => action().GetAwaiter().GetResult());

    private void RunMessageLoop()
    {
        try
        {
            var app = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown,
            };
            _ready.Set();
            app.Run();
        }
        catch (Exception ex)
        {
            _startupError = ex;
            _ready.Set();
        }
    }

    private static void Send(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("WPF dispatcher is unavailable.");
        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action, DispatcherPriority.Normal);
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (Application.Current is not null)
        {
            Send(() => Application.Current.Shutdown());
            _uiThread.Join(TimeSpan.FromSeconds(10));
        }

        _ready.Dispose();
    }
}

public abstract class WpfApplicationTestBase
{
    private static readonly Lazy<WpfSession> SharedSession = new(() => new WpfSession());

    protected WpfSession Session { get; } = SharedSession.Value;

    protected void RunOnUi(Action body) => Session.Invoke(body);

    protected void RunOnUiAsync(Func<Task> body) => Session.InvokeAsync(body);
}
