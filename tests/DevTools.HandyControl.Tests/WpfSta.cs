using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace DevTools.HandyControl.Tests;

internal static class WpfSta
{
    private static readonly Thread Thread;
    private static readonly Dispatcher Dispatcher;

    static WpfSta()
    {
        Dispatcher? dispatcher = null;
        Exception? startupError = null;
        using var ready = new ManualResetEventSlim(false);
        Thread = new Thread(() =>
        {
            try
            {
                _ = new System.Windows.Application
                {
                    ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown,
                };
                dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex)
            {
                startupError = ex;
            }
            finally
            {
                ready.Set();
            }

            if (startupError is null)
            {
                Dispatcher.Run();
            }
        })
        {
            IsBackground = true,
            Name = "DevTools.HandyControl.Tests",
        };
        Thread.SetApartmentState(ApartmentState.STA);
        Thread.Start();
        ready.Wait();
        if (startupError is not null)
        {
            ExceptionDispatchInfo.Capture(startupError).Throw();
        }

        Dispatcher = dispatcher ?? throw new InvalidOperationException("WPF dispatcher did not start.");
    }

    public static void Invoke(Action action)
    {
        Exception? error = null;
        Dispatcher.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
