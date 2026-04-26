using System.Windows.Threading;
using Autodesk.Windows;
namespace DevTools.Utilities;

/// <summary>
/// Helper class for dispatching actions to the Autodesk UI thread.
/// Uses ComponentManager.Ribbon.Dispatcher which is the official Autodesk UI dispatcher.
/// </summary>
[PublicAPI]
public static class DispatcherHelper
{
    /// <summary>
    /// Gets the Autodesk UI thread dispatcher.
    /// </summary>
    private static Dispatcher? RevitDispatcher => ComponentManager.Ribbon?.Dispatcher;

    /// <summary>
    /// Executes an action on the Autodesk main UI thread.
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
    /// Executes an action on the Autodesk main UI thread with specified priority.
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
