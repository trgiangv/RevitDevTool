using DevTools.ModelsContext.Explorer;
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

internal static class ExplorerShotRun
{
    public static async Task<int> CaptureAsync(SandboxWindow window, Application app, string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            await window.MeasureAsync(SandboxSize.Hundred, _ => { });
            var run = window.CurrentRun!;
            var panel = run.Panel;
            var explorer = panel.Explorer;
            var view = (ModelContextView)window.Host.Content;
            var explorerView = (ExplorerTabView)view.FindName("ExplorerView");
            var section = (CategorySection)explorerView.FindName("CategorySection");
            var list = (ListBox)explorerView.FindName("RowList");
            var theme = ThemeManager.Current.ActualApplicationTheme;

            panel.ContextBar.Scope = ScopeKind.ActiveView;
            panel.ContextBar.Check([FakeModelGenerator.Current, FakeModelGenerator.Mep]);
            await panel.RefreshAsync();
            await Settle(800);
            explorer.CollapseTree();

            section.IsOpen = true;
            var hidden = explorer.Categories.Picker.Items.First(item => item.Name == "Ducts");
            await hidden.ToggleEyeCommand.ExecuteAsync(null);
            explorer.Categories.Picker.Items.First(item => item.Name == "Doors").IsChecked = false;
            await Settle(800);
            Save(window, directory, $"{theme}-explorer-categories.png");

            section.IsOpen = false;
            var type = await SmallHiddenTypeAsync(explorer);
            list.SelectedItems.Clear();
            list.SelectedItems.Add(type);
            list.ScrollIntoView(type);
            await Settle(800);
            Save(window, directory, $"{theme}-explorer-hidden-selected.png");

            var dimmed = explorer.Rows.First(row => row.Parent == type && row.IsDimmed);
            list.SelectedItems.Clear();
            list.SelectedItems.Add(dimmed);
            list.ScrollIntoView(type);
            list.ScrollIntoView(type.Children!.Where(row => row.IsDimmed).Skip(4).FirstOrDefault() ?? dimmed);
            await Settle(800);
            Save(window, directory, $"{theme}-explorer-hidden.png");

            run.Model.PickGate = new TaskCompletionSource<bool>();
            var picking = explorer.Pick.PickCommand.ExecuteAsync(null);
            await Settle(600);
            Save(window, directory, $"{theme}-explorer-picking.png");
            run.Model.PickGate.SetResult(true);
            await picking;
            await Settle(600);
            Save(window, directory, $"{theme}-explorer-picked.png");
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

    private static async Task<TreeRow> SmallHiddenTypeAsync(ExplorerTabViewModel explorer)
    {
        var current = explorer.Rows.First(row => row.Kind == TreeRowKind.CurrentModelHeader);
        if (!current.IsExpanded)
        {
            await explorer.ExpandAsync(current);
        }

        TreeRow? best = null;
        foreach (var group in current.Children!.Where(row => row is { HiddenCount: > 0, Count: > 0 }).ToList())
        {
            await explorer.ExpandAsync(group);
            foreach (var family in group.Children!.Where(row => row is { HiddenCount: > 0, Count: > 0 }).ToList())
            {
                await explorer.ExpandAsync(family);
                foreach (var type in family.Children!.Where(row => row is { HiddenCount: > 0, Count: > 0 }))
                {
                    if (best is null || type.Count + type.HiddenCount < best.Count + best.HiddenCount)
                    {
                        best = type;
                    }
                }

                explorer.Collapse(family);
            }

            explorer.Collapse(group);
        }

        if (best is null)
        {
            throw new InvalidOperationException("No type with hidden elements.");
        }

        await explorer.ExpandAsync(best.Parent!.Parent!);
        await explorer.ExpandAsync(best.Parent!);
        await explorer.ExpandAsync(best);
        return best;
    }
    private static void Save(Window window, string directory, string name)
    {
        ShotRun.Save(window, Path.Combine(directory, name));
    }

    private static Task Settle(int milliseconds)
    {
        return Task.Delay(milliseconds);
    }
}
