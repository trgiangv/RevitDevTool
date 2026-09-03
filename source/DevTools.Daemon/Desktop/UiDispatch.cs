using Aprillz.MewUI;
namespace DevTools.Daemon.Desktop;

internal static class UiDispatch
{
    public static void Post(Action action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher is null || dispatcher.IsOnUIThread)
            action();
        else
            dispatcher.BeginInvoke(action);
    }

    public static void Send(Action action)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher is null || dispatcher.IsOnUIThread)
            action();
        else
            dispatcher.Invoke(action);
    }
}
