using DevTools.Presentation.Views.ModelsContext.Filters;
using DevTools.Presentation.Views.ModelsContext.Panel;
using DevTools.UI.Theme;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DevTools.ModelsContext.Sandbox;

internal static class ResultShotRun
{
    public static async Task<int> CaptureAsync(SandboxWindow window, Application app, string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            await window.MeasureAsync(SandboxSize.Hundred, _ => { });
            var panel = window.CurrentRun!.Panel;
            var view = (ModelContextView)window.Host.Content;
            var name = $"{ThemeManager.Current.ActualApplicationTheme}-{window.Width:0}";

            panel.IsFilterTab = true;
            await Task.Delay(400);
            panel.Filter.Library.AddCommand.Execute(null);
            await Task.Delay(600);
            foreach (var item in panel.Filter.Categories.Items.Take(3))
            {
                item.IsChecked = true;
            }

            await Task.Delay(1500);
            ShotRun.Save(window, Path.Combine(directory, $"{name}-result.png"));

            panel.Filter.Runner.Mode = DevTools.ModelsContext.Filters.ResultSelectionMode.Remove;
            await Task.Delay(600);
            ShotRun.Save(window, Path.Combine(directory, $"{name}-result-remove.png"));

            var filterView = (FilterTabView)view.FindName("FilterView");
            var tools = (ToolBar)filterView.FindName("ResultTools");
            if (tools.HasOverflowItems)
            {
                tools.IsOverflowOpen = true;
                await Task.Delay(600);
                var popupChild = (FrameworkElement)((FrameworkElement)tools.Template.FindName("PART_ToolBarOverflowPanel", tools)).Parent;
                SavePopup(popupChild, Path.Combine(directory, $"{name}-result-overflow.png"));
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
    private static void SavePopup(FrameworkElement element, string path)
    {
        const double Scale = 2;
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(element.ActualWidth * Scale), (int)(element.ActualHeight * Scale),
            96 * Scale, 96 * Scale, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
