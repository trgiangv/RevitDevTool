using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Visibility;
using DevTools.Presentation.Views.ModelsContext.Explorer;
using DevTools.Presentation.Views.ModelsContext.Filters;
using DevTools.Presentation.Views.ModelsContext.Panel;
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
        var picker = explorer.Categories.Picker;
        var category = picker.Items[0];
        picker.CheckNoneCommand.Execute(null);
        category.IsChecked = true;
        await explorer.Categories.ColorCommand.ExecuteAsync(null);
        await menu.ApplyColorCommand.ExecuteAsync(new RgbColor(0x2E, 0x9E, 0x5B));
        picker.CheckAllCommand.Execute(null);

        foreach (var expanded in explorer.Rows.Where(row => row.IsExpanded && row.Level > 0).ToList())
        {
            explorer.Collapse(expanded);
        }

        var current = explorer.Rows.First(row => row.Kind == TreeRowKind.CurrentModelHeader);
        if (!current.IsExpanded)
        {
            await explorer.ExpandAsync(current);
        }
        var groups = explorer.Rows.Where(row => row.Kind == TreeRowKind.Group && row.Document is { IsCurrentModel: true })
            .ToList();
        var colored = groups.First(row => row.Id != category.GroupId);
        var halftone = groups.First(row => row.Id != category.GroupId && row != colored);
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

    }

    private static async Task CaptureFilterAsync(SandboxWindow window, ModelContextViewModel panel, ModelContextView view,
        string directory, AppTheme theme)
    {
        panel.IsFilterTab = true;
        var filterView = (FilterTabView)view.FindName("FilterView");
        var filter = panel.Filter;
        filter.Library.AddCommand.Execute(null);
        await Settle(500);
        filter.Library.EditName = " ";
        await Settle(500);
        Save(window, Path.Combine(directory, $"{theme}-filter-name-error.png"));
        filter.Library.CancelRename();
        foreach (var category in filter.Categories.Items.Take(3))
        {
            category.IsChecked = true;
        }

        var toggle = (CategorySection)filterView.FindName("CategorySection");
        toggle.IsOpen = true;
        await Settle(700);
        Save(window, Path.Combine(directory, $"{theme}-filter-categories.png"));
        var mode = panel.Filter.Runner.Mode;
        panel.Filter.Runner.Mode = Filters.ResultSelectionMode.Remove;
        await Settle();
        Save(window, Path.Combine(directory, $"{theme}-filter-remove.png"));
        panel.Filter.Runner.Mode = mode;

        toggle.IsOpen = false;
        filter.Root.AddConditionCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];
        rule.SelectedParameter = rule.Parameters.FirstOrDefault();
        rule.Value = "SA";
        rule.IsCaseSensitive = true;
        filter.Root.AddGroupCommand.Execute(null);
        var set = (RuleSetViewModel)filter.Root.Children[1];
        set.Match = SetMatch.Any;
        set.AddConditionCommand.Execute(null);
        ((RuleViewModel)set.Children[0]).ParameterName = "Level";
        await Settle(700);
        Save(window, Path.Combine(directory, $"{theme}-filter-rules.png"));

        await CaptureKindsAsync(window, filter, filterView, directory, theme);
    }

    private static async Task CaptureKindsAsync(SandboxWindow window, FilterTabViewModel filter, FilterTabView filterView,
        string directory, AppTheme theme)
    {
        for (var wait = 0; wait < 40 && !filter.Root.Parameters.Any(parameter => parameter.DisplayName == "Width"); wait++)
        {
            await Settle(250);
        }

        filter.Root.Children.Clear();
        var names = new[] { "Mark", "Width", "Flag", "Level" };
        foreach (var name in names)
        {
            filter.Root.AddConditionCommand.Execute(null);
            var rule = (RuleViewModel)filter.Root.Children[^1];
            rule.SelectedParameter = name == "Flag"
                ? new ParameterInfo(ParameterKey.Named("Flag", "spec:yesno"), "Flag", ValueKind.YesNo,
                    Origins: ParameterOrigin.Project, Bindings: ParameterBinding.Instance, SpecLabel: "Yes/No")
                : rule.Parameters.FirstOrDefault(parameter => parameter.DisplayName == name) ?? rule.Parameters.First();
            if (rule.Kind is ValueKind.Reference or ValueKind.Enumerated)
            {
                await rule.LoadValuesCommand.ExecuteAsync(null);
            }
        }

        await Settle(700);
        Save(window, Path.Combine(directory, $"{theme}-filter-kinds.png"));

        var box = FindBox(filterView, "Filter.Rule.Parameter");
        box.IsDropDownOpen = true;
        await Settle(700);
        var drop = box.Template.FindName("dropDownBorder", box) as FrameworkElement;
        Save(window, Path.Combine(directory, $"{theme}-filter-picker.png"), drop);
        box.IsDropDownOpen = false;

        await CaptureLogicAsync(window, filter, filterView, directory, theme);
    }

    private static async Task CaptureLogicAsync(SandboxWindow window, FilterTabViewModel filter, FilterTabView filterView,
        string directory, AppTheme theme)
    {
        FilterExamples.UpperLevelDoorsWithoutMark(filter);
        await Settle(700);
        Save(window, Path.Combine(directory, $"{theme}-filter-logic.png"));

        FilterExamples.ArchWallsOrMepDucts(filter);
        await Settle(700);
        Save(window, Path.Combine(directory, $"{theme}-filter-scope.png"));

        var walls = (RuleSetViewModel)filter.Root.Children[0];
        walls.SetCategoryScope([FilterExamples.Walls, FilterExamples.Doors]);
        await Settle(500);
        var box = Find<DevTools.UI.Controls.TagCheckComboBox>(filterView, "Filter.Set.Scope.Categories");
        box.IsDropDownOpen = true;
        await Settle(700);
        var drop = (box.Template.FindName("PART_Popup", box) as System.Windows.Controls.Primitives.Popup)?.Child as FrameworkElement;
        Save(window, Path.Combine(directory, $"{theme}-filter-scope-open.png"), drop);
        box.IsDropDownOpen = false;
        walls.SetCategoryScope(filter.Categories.Items.Select(item => item.GroupId).Take(8));
        await Settle(500);
        Save(window, Path.Combine(directory, $"{theme}-filter-scope-warning.png"));

        FilterExamples.NestedWideScopes(filter);
        await Settle(700);
        Save(window, Path.Combine(directory, $"{theme}-filter-tags.png"));
    }

    private static ComboBox FindBox(DependencyObject root, string automationId)
    {
        return Find<ComboBox>(root, automationId);
    }

    private static T Find<T>(DependencyObject root, string automationId)
        where T : FrameworkElement
    {
        var queue = new Queue<DependencyObject>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current is T { IsVisible: true } box && System.Windows.Automation.AutomationProperties.GetAutomationId(box) == automationId)
            {
                return box;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
            {
                queue.Enqueue(VisualTreeHelper.GetChild(current, index));
            }
        }

        throw new InvalidOperationException(automationId);
    }

    private static Task Settle(int milliseconds = 300)
    {
        return Task.Delay(milliseconds);
    }

    internal static void Save(Window window, string path, FrameworkElement? menu = null)
    {
        const double Scale = 2;
        var width = window.ActualWidth;
        var height = window.ActualHeight;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new VisualBrush(window), null, new Rect(0, 0, width, height));
            if (menu is not null && PresentationSource.FromVisual(menu) is { } source)
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
