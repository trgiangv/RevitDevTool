using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;
using DevTools.Presentation.Views.ModelsContext;
using DevTools.UI.Theme;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;

namespace DevTools.ModelsContext.Views.Tests;

internal sealed class PanelUi : IDisposable
{
    private static readonly Lazy<FakeModelData> Small = new(() => FakeModelGenerator.Generate(new FakeModelOptions
    {
        CurrentElements = 5_000,
        LinkElements = 2_000,
    }));

    private PanelUi(FakeHost fake, FakeClock clock, ModelContextViewModel panel, ModelContextView view, ThemeHost host)
    {
        Model = fake.Model;
        Actions = fake.Actions;
        Clock = clock;
        Panel = panel;
        View = view;
        Host = host;
    }

    public FakeModel Model { get; }

    public FakeViewActions Actions { get; }

    public FakeClock Clock { get; }

    public ModelContextViewModel Panel { get; }

    public ModelContextView View { get; }

    public ThemeHost Host { get; }

    public static PanelUi Open(double width = 320, AppTheme theme = AppTheme.Dark)
    {
        var fake = new FakeHost(new FakeModel(Small.Value));
        var clock = new FakeClock();
        var (panel, view) = WpfSta.Invoke(() =>
        {
            var panel = new ModelContextViewModel(fake.Services(clock, clock));
            panel.ContextBar.Scope = ScopeKind.WholeModel;
            return (panel, new ModelContextView { DataContext = panel });
        });
        WpfSta.Run(() => panel.InitializeAsync(CancellationToken.None));
        return new PanelUi(fake, clock, panel, view, ThemeHost.Show(view, width, theme: theme));
    }

    public void Press(string automationId)
    {
        Host.FindInWindow(automationId).AsButton().Invoke();
    }

    public void PressIn(AutomationElement row, string automationId)
    {
        var button = Retry.WhileNull(
            () => row.FindFirstDescendant(cf => cf.ByAutomationId(automationId)),
            TimeSpan.FromSeconds(5),
            ignoreException: true).Result;
        Assert.IsNotNull(button, automationId + " was not found in the row.");
        button.AsButton().Invoke();
    }

    public System.Windows.Controls.ContextMenu OpenRowMenu(string rowName)
    {
        var menu = WpfSta.Invoke(() =>
        {
            var explorer = (ExplorerTabView)View.FindName("ExplorerView");
            var list = (System.Windows.Controls.Control)explorer.FindName("RowList");
            var row = Panel.Explorer.Rows.First(row => row.Text == rowName && row.Kind == TreeRowKind.Group);
            Panel.Explorer.OpenMenu(row);
            list.ContextMenu!.PlacementTarget = list;
            list.ContextMenu.IsOpen = true;
            return list.ContextMenu;
        });
        Assert.IsTrue(ThemeHost.WaitFor(() => menu.IsOpen), "The row menu did not open.");
        return menu;
    }

    public void Collapse(string header)
    {
        PressIn(Host.Row(header), "Explorer.Row.Expand");
    }

    public void ShowView(string automationId)
    {
        Host.FindInWindow(automationId).Patterns.SelectionItem.Pattern.Select();
    }

    public bool Has(AutomationElement row, string automationId)
    {
        return row.FindFirstDescendant(cf => cf.ByAutomationId(automationId)) is not null;
    }

    public void Dispose()
    {
        Host.Dispose();
        WpfSta.Invoke(Panel.Dispose);
    }
}
