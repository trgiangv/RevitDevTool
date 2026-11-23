using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;
using DevTools.Presentation.Views.ModelsContext.Explorer;
using DevTools.Presentation.Views.ModelsContext.Panel;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using System.Windows;
using System.Windows.Input;

namespace DevTools.ModelsContext.Views.Tests.Panel;

[TestClass]
[DoNotParallelize]
public sealed class WhyHiddenViewTests
{
    private static readonly Lazy<FakeModelData> Data = new(() => FakeModelGenerator.Generate(new FakeModelOptions
    {
        CurrentElements = 2_000,
        LinkElements = 500,
    }));

    [TestMethod]
    public void StripHiddenUntilOpened()
    {
        using var ui = StripUi.Open();

        Assert.IsNull(ui.Host.TryFindInWindow("WhyHiddenStrip"));
        ui.Open([HiddenCues.TemporaryHideIsolate]);

        Assert.IsNotNull(ui.Host.FindInWindow("WhyHiddenStrip"));
        Assert.AreEqual(HiddenCues.TemporaryHideIsolate.Text, ui.Host.FindInWindow("WhyHiddenCause").Name);
        Assert.IsNotNull(ui.Host.FindInWindow("WhyHiddenFix"));
        Assert.IsNull(ui.Host.TryFindInWindow("WhyHiddenExpand"));
    }

    [TestMethod]
    public void CategoryCueHasNoFix()
    {
        using var ui = StripUi.Open();

        ui.Open([HiddenCues.Category("Ducts")]);

        Assert.IsNull(ui.Host.TryFindInWindow("WhyHiddenFix"));
    }

    [TestMethod]
    public void NoCauseHasNoFix()
    {
        using var ui = StripUi.Open();

        ui.Open([]);

        Assert.AreEqual(HiddenCues.NoneFound.Text, ui.Host.FindInWindow("WhyHiddenCause").Name);
        Assert.IsNull(ui.Host.TryFindInWindow("WhyHiddenFix"));
    }

    [TestMethod]
    public void ExpandListsAllCauses()
    {
        using var ui = StripUi.Open();
        ui.Open([HiddenCues.HiddenInView, HiddenCues.Filter("Walls-Demo"), HiddenCues.OutsideCrop]);

        ui.Host.FindInWindow("WhyHiddenExpand").Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.WhyHidden.IsExpanded), "The chevron did not expand.");
        var list = ui.Host.FindInWindow("WhyHiddenList");
        Assert.IsTrue(ThemeHost.WaitForUi(() => list.FindAllDescendants(cf => cf.ByAutomationId("WhyHiddenItem")).Length == 3),
            "The list does not show every cause.");
    }

    [TestMethod]
    public void FixButtonShowsAndCloses()
    {
        using var ui = StripUi.Open();
        ui.Explainer.CausesOf = _ => ui.Explainer.Calls.Count == 1 ? [HiddenCues.TemporaryHideIsolate] : [];
        ui.Open(null);

        ui.Host.FindInWindow("WhyHiddenFix").AsButton().Invoke();

        Assert.IsTrue(ThemeHost.WaitFor(() => !ui.Panel.WhyHidden.IsOpen), "The strip stayed open after the fix.");
        Assert.AreEqual(1, ui.Actions.ResetCount);
    }

    [TestMethod]
    public void CloseButtonClosesStrip()
    {
        using var ui = StripUi.Open();
        ui.Open([HiddenCues.OutsideCrop]);

        ui.Host.FindInWindow("WhyHiddenClose").AsButton().Invoke();

        Assert.IsTrue(ThemeHost.WaitFor(() => !ui.Panel.WhyHidden.IsOpen), "The close button left the strip open.");
    }

    [TestMethod]
    public void EscapeClosesStrip()
    {
        using var ui = StripUi.Open();
        ui.Open([HiddenCues.OutsideCrop]);

        WpfSta.Invoke(() =>
        {
            var source = PresentationSource.FromVisual(ui.View)!;
            ui.View.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape)
            {
                RoutedEvent = Keyboard.KeyDownEvent,
            });
        });

        Assert.IsTrue(ThemeHost.WaitFor(() => !ui.Panel.WhyHidden.IsOpen), "Esc left the strip open.");
    }

    [TestMethod]
    public void ErrorShowsOneCue()
    {
        using var ui = StripUi.Open();
        ui.Explainer.Error = new InvalidOperationException("Element gone.");

        ui.Open(null);

        Assert.AreEqual(WhyHiddenStripViewModel.FailedText, ui.Host.FindInWindow("WhyHiddenCause").Name);
        Assert.AreEqual("Element gone.", ui.Host.FindInWindow("WhyHiddenCue").Name);
    }

    [TestMethod]
    public void StripSitsAboveTemporaryBar()
    {
        using var ui = StripUi.Open();
        WpfSta.Run(() => ui.Panel.Explorer.ToggleEyeCommand.ExecuteAsync(ui.Ducts()));
        ui.Open([HiddenCues.TemporaryHideIsolate]);

        var strip = ui.Host.FindInWindow("WhyHiddenStrip").BoundingRectangle;
        var keep = ui.Host.FindInWindow("Temporary.Keep").BoundingRectangle;

        Assert.IsLessThanOrEqualTo(keep.Top, strip.Bottom, "The strip overlaps the Temporary bar.");
    }

    [TestMethod]
    public void MenuWhyHiddenNeedsOneElement()
    {
        using var ui = StripUi.Open();

        var menu = ui.OpenDuctsMenu();

        var item = ui.Host.FindInMenu(menu, "Menu.WhyHidden");
        Assert.AreEqual(ControlType.MenuItem, item.ControlType);
        Assert.IsFalse(item.IsEnabled);
        WpfSta.Invoke(() => menu.IsOpen = false);
    }

    private sealed class StripUi : IDisposable
    {
        private StripUi(FakeHost fake, ModelContextViewModel panel, ModelContextView view, ThemeHost host)
        {
            Explainer = fake.Explainer;
            Actions = fake.Actions;
            Panel = panel;
            View = view;
            Host = host;
        }

        public FakeVisibilityExplainer Explainer { get; }

        public FakeViewActions Actions { get; }

        public ModelContextViewModel Panel { get; }

        public ModelContextView View { get; }

        public ThemeHost Host { get; }

        public static StripUi Open()
        {
            var fake = new FakeHost(new FakeModel(Data.Value));
            var clock = new FakeClock();
            var (panel, view) = WpfSta.Invoke(() =>
            {
                var panel = new ModelContextViewModel(fake.Services(clock, clock));
                panel.ContextBar.Scope = ScopeKind.WholeModel;
                return (panel, new ModelContextView { DataContext = panel });
            });
            WpfSta.Run(() => panel.InitializeAsync(CancellationToken.None));
            return new StripUi(fake, panel, view, ThemeHost.Show(view));
        }

        public TreeRow Ducts()
        {
            return WpfSta.Invoke(() => Panel.Explorer.Rows.First(row =>
                row.Kind == TreeRowKind.Group && row.Document == FakeModelGenerator.Current && row.Text == "Ducts"));
        }

        public void Open(IReadOnlyList<HiddenCause>? causes)
        {
            if (causes is not null)
            {
                Explainer.Causes = causes;
            }

            var element = WpfSta.Invoke(() => Panel.Explorer.ElementsOf([Ducts()]).First());
            WpfSta.Run(() => Panel.WhyHidden.OpenAsync(element));
            Assert.IsTrue(ThemeHost.WaitFor(() => Panel.WhyHidden is { IsOpen: true, IsBusy: false }), "The strip did not open.");
        }

        public System.Windows.Controls.ContextMenu OpenDuctsMenu()
        {
            var menu = WpfSta.Invoke(() =>
            {
                var explorer = (ExplorerTabView)View.FindName("ExplorerView");
                var list = (System.Windows.Controls.Control)explorer.FindName("RowList");
                Panel.Explorer.OpenMenu(Ducts());
                list.ContextMenu!.PlacementTarget = list;
                list.ContextMenu.IsOpen = true;
                return list.ContextMenu;
            });
            Assert.IsTrue(ThemeHost.WaitFor(() => menu.IsOpen), "The row menu did not open.");
            return menu;
        }

        public void Dispose()
        {
            Host.Dispose();
            WpfSta.Invoke(Panel.Dispose);
        }
    }
}
