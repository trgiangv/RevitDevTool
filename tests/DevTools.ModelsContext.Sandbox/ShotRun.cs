using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.ViewModels;
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
            var explorer = panel.Explorer;
            var view = (ModelContextView)window.Host.Content;
            var list = (ListBox)((ExplorerTabView)view.FindName("ExplorerView")).FindName("RowList");

            await explorer.ToggleExpandCommand.ExecuteAsync(explorer.Rows.First(row => row.Kind == TreeRowKind.AllModelsHeader));
            var theme = ThemeManager.Current.ActualApplicationTheme;
            {
                var instance = explorer.Rows.First(row => row.Kind == TreeRowKind.Instance);
                explorer.OpenMenu(instance);
                list.ScrollIntoView(instance);
                list.UpdateLayout();
                await Settle(700);
                Save(window, Path.Combine(directory, $"{theme}-instance.png"));

                var owner = instance;
                while (owner.Kind != TreeRowKind.Group)
                {
                    owner = owner.Parent!;
                }

                explorer.Collapse(owner);
                list.ScrollIntoView(owner);
                await Settle(500);
                Save(window, Path.Combine(directory, $"{theme}-badge.png"));

                var group = explorer.Rows.First(row => row.Kind == TreeRowKind.Group && row != owner);
                explorer.OpenMenu(group);
                list.ScrollIntoView(group);
                await Settle(700);
                Save(window, Path.Combine(directory, $"{theme}-category.png"));

                explorer.OpenMenu(instance);
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

            await CaptureOverridesAsync(window, panel, list, directory, theme);
            await CaptureFilterAsync(window, panel, view, directory, theme);
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

    private static async Task CaptureOverridesAsync(SandboxWindow window, ModelContextViewModel panel, ListBox list,
        string directory, AppTheme theme)
    {
        var explorer = panel.Explorer;
        var menu = panel.ContextMenu;
        var header = explorer.Rows.First(row => row.Kind == TreeRowKind.AllModelsHeader);
        await explorer.ExpandAsync(header);
        var category = explorer.Rows.First(row => row.Kind == TreeRowKind.AllModelsGroup);
        explorer.OpenMenu(category);
        await menu.ApplyColorCommand.ExecuteAsync(new RgbColor(0x2E, 0x9E, 0x5B));

        foreach (var expanded in explorer.Rows.Where(row => row.IsExpanded && row.Level > 0).ToList())
        {
            explorer.Collapse(expanded);
        }

        explorer.Collapse(header);
        await explorer.ExpandAsync(explorer.Rows.First(row => row.Kind == TreeRowKind.CurrentModelHeader));
        var groups = explorer.Rows.Where(row => row.Kind == TreeRowKind.Group && row.Document is { IsCurrentModel: true })
            .ToList();
        var colored = groups.First(row => row.Id != category.Id);
        var halftone = groups.First(row => row.Id != category.Id && row != colored);
        explorer.OpenMenu(colored);
        await menu.ApplyColorCommand.ExecuteAsync(new RgbColor(0xE0, 0x4F, 0x39));
        explorer.OpenMenu(halftone);
        await menu[MenuAction.Halftone].Command.ExecuteAsync(null);
        await explorer.ExpandAsync(colored);

        list.SelectedItems.Clear();
        list.ScrollIntoView(explorer.Rows[0]);
        await Settle(700);
        Save(window, Path.Combine(directory, $"{theme}-overrides.png"));

        list.SelectedItems.Add(colored);
        list.SelectedItems.Add(halftone);
        await Settle(700);
        Save(window, Path.Combine(directory, $"{theme}-overrides-selected.png"));

        list.SelectedItems.Clear();
        await explorer.ExpandAsync(header);
        list.ScrollIntoView(explorer.Rows[0]);
        await Settle(500);
        Save(window, Path.Combine(directory, $"{theme}-overrides-category.png"));
    }

    private static async Task CaptureFilterAsync(SandboxWindow window, ModelContextViewModel panel, ModelContextView view,
        string directory, AppTheme theme)
    {
        panel.IsFilterTab = true;
        var filterView = (FilterTabView)view.FindName("FilterView");
        var filter = panel.Filter;
        filter.AddCommand.Execute(null);
        await Settle(500);
        filter.EditName = " ";
        await Settle(500);
        Save(window, Path.Combine(directory, $"{theme}-filter-name-error.png"));
        filter.CancelRename();
        foreach (var category in filter.Categories.Take(3))
        {
            category.IsChecked = true;
        }

        var toggle = (ToggleButton)filterView.FindName("CategoriesToggle");
        toggle.IsChecked = true;
        await Settle(700);
        Save(window, Path.Combine(directory, $"{theme}-filter-categories.png"));
        var mode = panel.Filter.Mode;
        panel.Filter.Mode = ViewModels.ResultSelectionMode.Remove;
        await Settle();
        Save(window, Path.Combine(directory, $"{theme}-filter-remove.png"));
        panel.Filter.Mode = mode;

        toggle.IsChecked = false;
        filter.Root.AddRuleCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];
        rule.SelectedParameter = rule.Parameters.FirstOrDefault();
        rule.Value = "SA";
        rule.Kind = ValueKind.ElementId;
        filter.Root.AddSetCommand.Execute(null);
        var set = (RuleSetViewModel)filter.Root.Children[1];
        set.Match = SetMatch.Any;
        set.AddRuleCommand.Execute(null);
        ((RuleViewModel)set.Children[0]).ParameterName = "Level";
        await Settle(700);
        Save(window, Path.Combine(directory, $"{theme}-filter-rules.png"));
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
