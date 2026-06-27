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
    /// Toggles WPF hardware/software rendering mode on the main UI thread.
    /// </summary>
    public static void ToggleHardwareRendering(bool useHardware)
    {
        RunOnMainThread(() =>
            RenderOptions.ProcessRenderMode = useHardware ? RenderMode.Default : RenderMode.SoftwareOnly);
    }

    /// <summary>
    /// https://github.com/Nice3point/RevitToolkit
    /// </summary>
    public static void RunWithMessagePump(Task task)
    {
        if (task.IsCompleted)
        {
            task.GetAwaiter().GetResult();
            return;
        }

        var frame = new DispatcherFrame();

        // TaskScheduler.Default ensures continuation runs on ThreadPool, not UI thread.
        // Prevents deadlock: if continuation ran on UI thread via SynchronizationContext,
        // it would wait for PushFrame to finish, which waits for continuation - deadlock.
        task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);

        Dispatcher.PushFrame(frame);

        task.GetAwaiter().GetResult();
    }
}
