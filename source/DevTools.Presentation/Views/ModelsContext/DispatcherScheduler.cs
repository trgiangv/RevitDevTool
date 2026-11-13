using DevTools.ModelsContext.Ports;
using System.Windows.Threading;

namespace DevTools.Presentation.Views.ModelsContext;

public sealed class DispatcherScheduler(Dispatcher dispatcher) : IScheduler
{
    public IDisposable Schedule(TimeSpan delay, Action action)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
        return new Handle(timer);
    }

    private sealed class Handle(DispatcherTimer timer) : IDisposable
    {
        public void Dispose()
        {
            timer.Stop();
        }
    }
}
