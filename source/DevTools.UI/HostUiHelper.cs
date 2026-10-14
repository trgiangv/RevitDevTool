using System.Windows.Threading;

namespace DevTools.UI;

/// <summary>
/// Helper class for dispatching actions to the Autodesk UI thread.
/// Uses ComponentManager.Ribbon.Dispatcher which is the official Autodesk UI dispatcher.
/// </summary>
[PublicAPI]
public static class HostUiHelper
{
    /// <summary>
    /// Gets the Autodesk UI thread dispatcher.
    /// </summary>
    public static Dispatcher? HostDispatcher { get; private set; }

    /// <summary>
    /// Gets the handle of the Host main window.
    /// </summary>
    public static IntPtr MainWindowHandle { get; private set; }

    public static void Initialize(IntPtr mainWindowHandle, Dispatcher dispatcher)
    {
        HostDispatcher = dispatcher;
        MainWindowHandle = mainWindowHandle;
    }

    /// <summary>
    /// Executes an action on the Autodesk main UI thread.
    /// If already on the UI thread, executes synchronously.
    /// </summary>
    public static void RunOnMainThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (HostDispatcher is null || HostDispatcher.CheckAccess())
            action();
        else
            HostDispatcher.BeginInvoke(action);
    }

    /// <summary>
    /// Observe a host <c>StartAsync</c> task that must already be complete.
    /// The synchronous prefix ran on the caller. An incomplete task means a
    /// hosted service awaited; blocking here deadlocks the CAD dispatcher,
    /// and dropping the task hides the fault.
    /// </summary>
    public static void RequireCompleted(Task start)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (!start.IsCompleted)
        {
            throw new InvalidOperationException(
                "IHostedService.StartAsync must not await. Runtime init stays on the background task.");
        }

        start.GetAwaiter().GetResult();
    }
}
