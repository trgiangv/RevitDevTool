using System.Windows;
using System.Windows.Interop;
using DevTools.UI;
using RevitDevTool.Core;
namespace RevitDevTool.Tools.Helpers;

/// <summary>
/// Shows tool windows once per key at the top-left of the active UIView.
/// If a window of that type is already open, <see cref="Show"/> returns without creating another.
/// </summary>
public sealed class ToolWindowService
{
    private readonly Dictionary<string, Window> _windows = new(StringComparer.Ordinal);

    public bool IsOpen(string key) => _windows.TryGetValue(key, out var w) && w.IsVisible;

    public void Show(string key, Func<Window> windowFactory)
    {
        HostUiHelper.RunOnMainThread(() =>
        {
            if (IsOpen(key))
                return;

            var window = windowFactory();
            window.Closed += (_, _) => _windows.Remove(key);
            window.SetHostAppOwner();
            PositionTopLeftOfUiView(window);
            _windows[key] = window;
            window.Show();
        });
    }

    public void Close(string key)
    {
        HostUiHelper.RunOnMainThread(() =>
        {
            if (!_windows.Remove(key, out var window))
                return;

            try
            {
                window.Close();
            }
            catch
            {
                // already closing
            }
        });
    }

    public void CloseAll()
    {
        foreach (var key in _windows.Keys.ToList())
            Close(key);
    }

    private static void PositionTopLeftOfUiView(Window window)
    {
        const double margin = 12;
        window.WindowStartupLocation = WindowStartupLocation.Manual;

        var rect = GetActiveUiViewRectangle();
        if (rect is not null)
        {
            var source = HwndSource.FromHwnd(RevitContext.UiApplication.MainWindowHandle);
            var dpi = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
            window.Left = rect.Left * dpi + margin;
            window.Top = rect.Top * dpi + margin;
            return;
        }

        window.Left = margin;
        window.Top = 120;
    }

    private static Rectangle? GetActiveUiViewRectangle()
    {
        try
        {
            var uiDoc = RevitContext.UiApplication.ActiveUIDocument;
            if (uiDoc?.ActiveView is null)
                return null;

            foreach (var uiView in uiDoc.GetOpenUIViews())
            {
                if (uiView.ViewId == uiDoc.ActiveView.Id)
                    return uiView.GetWindowRectangle();
            }
        }
        catch
        {
            // no active view / host not ready
        }

        return null;
    }
}
