using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.Presentation.Views.ModelsContext.Explorer;
using DevTools.Presentation.Views.ModelsContext.Filters;
using DevTools.Presentation.Views.ModelsContext.Panel;
using DevTools.UI.Theme;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DevTools.ModelsContext.Sandbox;

internal static class CategoryShotRun
{
    public static async Task<int> CaptureAsync(SandboxWindow window, Application app, string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            await window.MeasureAsync(SandboxSize.Hundred, _ => { });
            var panel = window.CurrentRun!.Panel;
            var view = (ModelContextView)window.Host.Content;
            var theme = ThemeManager.Current.ActualApplicationTheme;

            panel.ContextBar.Scope = ScopeKind.ActiveView;
            panel.ContextBar.Tick([FakeModelGenerator.Current, FakeModelGenerator.Mep]);
            await panel.RefreshAsync();
            await Task.Delay(2500);

            var explorerView = (ExplorerTabView)view.FindName("ExplorerView");
            var explorerSection = (CategorySection)explorerView.FindName("CategorySection");
            explorerSection.IsOpen = true;
            await Task.Delay(400);
            var picker = panel.Explorer.Categories.Picker;
            ShowTree(picker);
            await Task.Delay(800);
            Reveal(explorerSection, picker);
            panel.Explorer.SearchText = "Toposolid";
            await Task.Delay(800);
            if (panel.Explorer.Rows.FirstOrDefault(row => row is { Kind: TreeRowKind.Group, Text: "Toposolid" }) is { } toposolid)
            {
                await panel.Explorer.ExpandAsync(toposolid);
            }

            await Task.Delay(800);
            ShotRun.Save(window, Path.Combine(directory, $"{theme}-explorer-categories.png"));

            panel.IsFilterTab = true;
            await Task.Delay(400);
            panel.Filter.Library.AddCommand.Execute(null);
            await Task.Delay(600);
            var filterView = (FilterTabView)view.FindName("FilterView");
            var filterSection = (CategorySection)filterView.FindName("CategorySection");
            filterSection.IsOpen = true;
            await Task.Delay(400);
            ShowTree(panel.Filter.Categories);
            await Task.Delay(400);
            Reveal(filterSection, panel.Filter.Categories);
            await Task.Delay(800);
            ShotRun.Save(window, Path.Combine(directory, $"{theme}-filter-categories.png"));
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

    private static void ShowTree(CategoryPickerViewModel picker)
    {
        CategoryItemViewModel Item(string name) => picker.Items.First(item => item.Name == name);
        Item("Toposolid").BoxState = true;
        Item("Toposolid").IsExpanded = true;
        Item("Railings").BoxState = true;
        Item("Top Rails").IsChecked = false;
        Item("Roofs").IsChecked = false;
        Item("Sprinklers").IsChecked = false;
    }

    private static void Reveal(CategorySection section, CategoryPickerViewModel picker)
    {
        var list = (ListBox)section.FindName("CategoryList");
        list.ScrollIntoView(picker.Items.First(item => item.Name == "Sub-divisions"));
    }
}
