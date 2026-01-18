using Autodesk.Windows;
using System.Windows.Threading;

namespace RevitDevTool.Utils;

/// <summary>
/// Helper class for dispatching actions to the Revit UI thread.
/// Uses ComponentManager.Ribbon.Dispatcher which is the official Revit UI dispatcher.
/// </summary>
[PublicAPI]
public static class DispatcherHelper
{
    /// <summary>
    /// Gets the Revit UI thread dispatcher.
    /// </summary>
    private static Dispatcher RevitDispatcher => ComponentManager.Ribbon.Dispatcher;

    /// <summary>
    /// Executes an action on the Revit main UI thread.
    /// If already on the UI thread, executes synchronously.
    /// </summary>
    public static void RunOnMainThread(Action action)
    {
        if (RevitDispatcher.CheckAccess())
            action();
        else
            RevitDispatcher.BeginInvoke(action);
    }

    /// <summary>
    /// Executes an action on the Revit main UI thread with specified priority.
    /// </summary>
    public static void RunOnMainThread(Action action, DispatcherPriority priority)
    {
        if (RevitDispatcher.CheckAccess())
            action();
        else
            RevitDispatcher.BeginInvoke(action, priority);
    }
}
