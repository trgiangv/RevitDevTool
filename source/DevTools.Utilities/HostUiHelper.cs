using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace DevTools.Utilities;

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
        if (HostDispatcher is null) return;
        if (HostDispatcher.CheckAccess())
            action();
        else
            HostDispatcher.BeginInvoke(action);
    }

    /// <summary>
    /// Executes an action on the Autodesk main UI thread with specified priority.
    /// </summary>
    public static void RunOnMainThread(Action action, DispatcherPriority priority)
    {
        if (HostDispatcher is null) return;
        if (HostDispatcher.CheckAccess())
            action();
        else
            HostDispatcher.BeginInvoke(action, priority);
    }

    /// <summary>
    /// Toggles WPF hardware/software rendering mode on the main UI thread.
    /// </summary>
    public static void ToggleHardwareRendering(bool useHardware)
    {
        RunOnMainThread(() =>
            RenderOptions.ProcessRenderMode = useHardware ? RenderMode.Default : RenderMode.SoftwareOnly);
    }
}
