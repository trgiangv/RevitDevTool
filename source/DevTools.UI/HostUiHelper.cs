using System.Windows.Interop;
using System.Windows.Media;
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
    /// Toggles WPF hardware/software rendering mode on the main UI thread.
    /// </summary>
    public static void ToggleHardwareRendering(bool useHardware)
    {
        RunOnMainThread(() =>
            RenderOptions.ProcessRenderMode = useHardware ? RenderMode.Default : RenderMode.SoftwareOnly);
    }

    /// <summary>
    /// Block on host start without pumping the WPF dispatcher.
    /// Clears <see cref="SynchronizationContext"/> <em>before</em> invoking
    /// <paramref name="start"/> so awaits resume on the thread pool instead of
    /// posting back to the blocked caller. The synchronous prefix of
    /// <paramref name="start"/> still runs on the caller
    /// </summary>
    public static void RunBlocking(Func<Task> start)
    {
        ArgumentNullException.ThrowIfNull(start);

        var captured = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            start().GetAwaiter().GetResult();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(captured);
        }
    }
}
