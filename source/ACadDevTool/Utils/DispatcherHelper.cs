using System.Windows.Threading;
using Autodesk.Windows;
namespace AcadDevTool.Utils;

/// <summary>
/// Helper class for dispatching actions to the AutoCad UI thread.
/// Uses ComponentManager.Ribbon.Dispatcher which is the official AutoCad UI dispatcher.
/// </summary>
[PublicAPI]
public static class DispatcherHelper
{
    /// <summary>
    /// Gets the AutoCad UI thread dispatcher.
    /// </summary>
    private static Dispatcher? RevitDispatcher => ComponentManager.Ribbon?.Dispatcher;

    /// <summary>
    /// Executes an action on the AutoCad main UI thread.
    /// If already on the UI thread, executes synchronously.
    /// </summary>
    public static void RunOnMainThread(Action action)
    {
        if (RevitDispatcher is null) return;
        if (RevitDispatcher.CheckAccess())
            action();
        else
            RevitDispatcher.BeginInvoke(action);
    }

    /// <summary>
    /// Executes an action on the AutoCad main UI thread with specified priority.
    /// </summary>
    public static void RunOnMainThread(Action action, DispatcherPriority priority)
    {
        if (RevitDispatcher is null) return;
        if (RevitDispatcher.CheckAccess())
            action();
        else
            RevitDispatcher.BeginInvoke(action, priority);
    }
}
