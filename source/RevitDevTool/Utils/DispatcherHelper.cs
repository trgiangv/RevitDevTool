using Autodesk.Windows;
using System.Windows.Threading;

namespace RevitDevTool.Utils;

/// <summary>
/// Helper class for dispatching actions to the Revit UI thread.
/// Uses ComponentManager.Ribbon.Dispatcher which is the official Revit UI dispatcher.
/// </summary>
public static class DispatcherHelper
{
    /// <summary>
    /// Gets the Revit UI thread dispatcher.
    /// </summary>
    private static Dispatcher Dispatcher => ComponentManager.Ribbon.Dispatcher;

    /// <summary>
    /// Executes an action on the Revit main UI thread.
    /// If already on the UI thread, executes synchronously.
    /// </summary>
    public static void RunOnMainThread(Action action)
    {
        if (Dispatcher.CheckAccess())
            action();
        else
            Dispatcher.BeginInvoke(action);
    }

    /// <summary>
    /// Executes an action on the Revit main UI thread with specified priority.
    /// </summary>
    public static void RunOnMainThread(Action action, DispatcherPriority priority)
    {
        if (Dispatcher.CheckAccess())
            action();
        else
            Dispatcher.BeginInvoke(action, priority);
    }
}
