using System.Runtime.ExceptionServices;
using Aprillz.MewUI;

namespace DevTools.Daemon.Tests.Support;

public sealed class MewUiSession : IDisposable
{
    private readonly Thread _uiThread;
    private readonly ManualResetEventSlim _ready = new(false);
    private Exception? _startupError;

    public MewUiSession()
    {
        _uiThread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "MewUiSession",
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
        _ready.Wait(TimeSpan.FromSeconds(30));
        if (_startupError is not null)
            ExceptionDispatchInfo.Capture(_startupError).Throw();
        if (!Application.IsRunning)
            throw new InvalidOperationException("MewUI application failed to start.");
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
            Application.Create()
                .UseWin32()
                .UseDirect2D()
                .UseTheme(ThemeVariant.System)
                .WithShutdownMode(ShutdownMode.OnExplicitShutdown)
                .OnStartup(() => _ready.Set())
                .Run();
        }
        catch (Exception ex)
        {
            _startupError = ex;
            _ready.Set();
        }
    }

    private static void Send(Action action)
    {
        var dispatcher = Application.Current.Dispatcher
            ?? throw new InvalidOperationException("MewUI dispatcher is unavailable.");
        if (dispatcher.IsOnUIThread)
            action();
        else
            dispatcher.Invoke(action);
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (Application.IsRunning)
        {
            Send(Application.Shutdown);
            _uiThread.Join(TimeSpan.FromSeconds(10));
        }

        _ready.Dispose();
    }
}

[CollectionDefinition(nameof(MewUiApplicationCollection), DisableParallelization = true)]
public sealed class MewUiApplicationCollection : ICollectionFixture<MewUiSession>;

public abstract class MewUiApplicationTestBase
{
    protected MewUiSession Session { get; }

    protected MewUiApplicationTestBase(MewUiSession session) => Session = session;

    protected void RunOnUi(Action body) => Session.Invoke(body);

    protected void RunOnUiAsync(Func<Task> body) => Session.InvokeAsync(body);
}
