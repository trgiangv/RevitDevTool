using DevTools.ModelsContext.Model;
using DevTools.Presentation.Views.ModelsContext;
using DevTools.UI.Theme;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DevTools.ModelsContext.Sandbox;

internal static class ShotRun
{
    public static async Task<int> CaptureAsync(SandboxWindow window, Application app, string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            await window.MeasureAsync(SandboxSize.Hundred, _ => { });
            var panel = window.CurrentRun!.Panel;
            var tree = panel.Tree;
            var view = (ModelContextView)window.Host.Content;
            var list = (ListBox)((TreeTabView)((TabItem)((TabControl)view.FindName("Tabs")).Items[0]).Content).FindName("RowList");

            await tree.ToggleExpandCommand.ExecuteAsync(tree.Rows.First(row => row.Kind == TreeRowKind.AllModelsHeader));
            var theme = ThemeManager.Current.ActualApplicationTheme;
            {
                var instance = tree.Rows.First(row => row.Kind == TreeRowKind.Instance);
                tree.OpenMenu(instance);
                list.ScrollIntoView(instance);
                list.UpdateLayout();
                await Settle(700);
                Save(window, Path.Combine(directory, $"{theme}-instance.png"));

                var owner = instance;
                while (owner.Kind != TreeRowKind.Group)
                {
                    owner = owner.Parent!;
                }

                tree.Collapse(owner);
                list.ScrollIntoView(owner);
                await Settle(500);
                Save(window, Path.Combine(directory, $"{theme}-badge.png"));

                var group = tree.Rows.First(row => row.Kind == TreeRowKind.Group && row != owner);
                tree.OpenMenu(group);
                list.ScrollIntoView(group);
                await Settle(700);
                Save(window, Path.Combine(directory, $"{theme}-category.png"));

                tree.OpenMenu(instance);
                list.ScrollIntoView(instance);
                list.ContextMenu!.PlacementTarget = list;
                list.ContextMenu.Placement = PlacementMode.Relative;
                list.ContextMenu.HorizontalOffset = 90;
                list.ContextMenu.VerticalOffset = 160;
                list.ContextMenu.IsOpen = true;
                await Settle(500);
                Save(window, Path.Combine(directory, $"{theme}-menu.png"), list.ContextMenu);
                list.ContextMenu.IsOpen = false;
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

    private static Task Settle(int milliseconds = 300)
    {
        return Task.Delay(milliseconds);
    }

    private static void Save(Window window, string path, ContextMenu? menu = null)
    {
        const double Scale = 2;
        var width = window.ActualWidth;
        var height = window.ActualHeight;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new VisualBrush(window), null, new Rect(0, 0, width, height));
            if (menu is { IsOpen: true } && PresentationSource.FromVisual(menu) is { } source)
            {
                var factor = source.CompositionTarget!.TransformToDevice.M11;
                var at = menu.PointToScreen(new Point(0, 0));
                var origin = window.PointToScreen(new Point(0, 0));
                var rect = new Rect((at.X - origin.X) / factor, (at.Y - origin.Y) / factor, menu.ActualWidth, menu.ActualHeight);
                context.DrawRectangle(new VisualBrush(menu), null, rect);
            }
        }

        var bitmap = new RenderTargetBitmap((int)(width * Scale), (int)(height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
