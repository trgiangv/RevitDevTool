using DevTools.ModelsContext.Tests.Shared;
using DevTools.Presentation.Views.ModelsContext;
using System.Diagnostics;
using System.Windows;

namespace DevTools.ModelsContext.Sandbox;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(new ConsoleTraceListener());
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        if (args.Contains("--trace", StringComparer.Ordinal))
        {
            ConsoleLog.Current = new ConsoleLog(Microsoft.Extensions.Logging.LogLevel.Trace);
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var shots = Array.IndexOf(args, "--shots");
        var drag = Array.IndexOf(args, "--drag");
        var dragShots = Array.IndexOf(args, "--drag-shots");
        var pickerShots = Array.IndexOf(args, "--picker-shots");
        var theme = Array.IndexOf(args, "--theme");
        var light = theme >= 0 && string.Equals(args[theme + 1], "light", StringComparison.OrdinalIgnoreCase);
        DevTools.UI.Theme.ThemeManager.Current.ApplySettingsTheme(light ? DevTools.UI.Theme.AppTheme.Light : DevTools.UI.Theme.AppTheme.Dark);

        var window = new SandboxWindow(autoLoad: !args.Contains("--measure", StringComparer.Ordinal) && shots < 0 && drag < 0 && dragShots < 0
                                                 && pickerShots < 0);
        var width = Array.IndexOf(args, "--width");
        if (width >= 0)
        {
            window.Width = double.Parse(args[width + 1], System.Globalization.CultureInfo.InvariantCulture);
        }
        var exitCode = 0;
        if (drag >= 0)
        {
            window.Loaded += async (_, _) => exitCode = await DragRun.MouseAsync(window, app, args[drag + 1]);
        }
        else if (dragShots >= 0)
        {
            window.Loaded += async (_, _) => exitCode = await DragRun.ShotsAsync(window, app, args[dragShots + 1]);
        }
        else if (pickerShots >= 0)
        {
            window.Loaded += async (_, _) => exitCode = await PickerShotRun.CaptureAsync(window, app, args[pickerShots + 1]);
        }
        else if (shots >= 0)
        {
            window.Loaded += async (_, _) => exitCode = await ShotRun.CaptureAsync(window, app, args[shots + 1]);
        }
        else if (args.Contains("--measure", StringComparer.Ordinal))
        {
            window.Loaded += async (_, _) => exitCode = await MeasureAsync(window, app);
        }
        else
        {
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        app.Run(window);
        return exitCode;
    }

    private static async Task<int> MeasureAsync(SandboxWindow window, Application app)
    {
        try
        {
            await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            window.ExtraParameters = FilterMeasure.ExtraParameters;
            foreach (var size in new[] { SandboxSize.Hundred, SandboxSize.Million })
            {
                Console.WriteLine($"== {size}");
                await window.MeasureAsync(size, Console.WriteLine);
                Console.WriteLine("-- filter");
                await FilterMeasure.RunAsync(window.CurrentRun!, (ModelContextView)window.Host.Content, window.Dispatcher,
                    Console.WriteLine);
                MeasureSnapshotMemory(size);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            app.Shutdown();
        }
    }

    private static void MeasureSnapshotMemory(SandboxSize size)
    {
        var data = SandboxRun.BuildData(size);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetTotalMemory(true);
        var snapshots = data.Sources.Values.Select(source => FakeModelGenerator.BuildSnapshot(source)).ToList();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var after = GC.GetTotalMemory(true);
        var elements = snapshots.Sum(snapshot => (long)snapshot.ElementCount);
        var estimated = snapshots.Sum(snapshot => snapshot.EstimatedBytes) / 1024d / 1024d;
        var heap = (after - before) / 1024d / 1024d;
        Console.WriteLine($"snapshot memory for {elements:N0} elements: array sizes {estimated:F1} MB = {estimated / elements * 1_000_000:F1} MB per million; GC heap delta {heap:F1} MB = {heap / elements * 1_000_000:F1} MB per million");
        GC.KeepAlive(snapshots);
    }
}
