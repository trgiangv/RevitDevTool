using DevTools.ModelsContext.Filters;
using DevTools.Presentation.Views.ModelsContext.Filters;
using FlaUI.Core.Tools;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace DevTools.ModelsContext.Views.Tests.Filters;

[TestClass]
[DoNotParallelize]
public sealed class ResultBarViewTests
{
    private static readonly string[] Tools4 = ["Filter.Result.Isolate", "Filter.Result.Halftone", "Filter.Result.Color", "Filter.Result.Eye"];

    [TestMethod]
    public void BarStaysOneLine()
    {
        using var ui = OpenWithResult(340);

        var ids = new[] { "Filter.ResultCount", "Filter.ResultLabel", "Filter.Mode", "Filter.Live", "ButtonOverflow" }
            .Concat(Tools4.Where(id => !IsOverflow(ui, id)));
        var boxes = ids.Select(id => ui.Host.FindInWindow(id).BoundingRectangle).ToList();

        Assert.IsLessThan(boxes.Min(box => box.Bottom), boxes.Max(box => box.Top), "The result bar spreads over more than one line.");
        Assert.IsFalse(IsOverflow(ui, "Filter.Result.Isolate"), "Isolate left the bar at 340 px.");
    }

    [TestMethod]
    public void WideBarShowsAllTools()
    {
        using var ui = OpenWithResult(380);

        Assert.IsFalse(WpfSta.Invoke(() => Tools(ui).HasOverflowItems), "A tool went to the overflow at 380 px.");
        foreach (var id in Tools4)
        {
            Assert.IsNotNull(ui.Host.TryFindInWindow(id), id + " is not in the bar.");
        }
    }
    [TestMethod]
    public void NarrowToolsOverflow()
    {
        using var ui = OpenWithResult(260, allCategories: true);
        Assert.IsGreaterThanOrEqualTo(1_000, WpfSta.Invoke(() => ui.Panel.Filter.Runner.ResultCount), "The count is not 4 digits.");
        Assert.IsTrue(ThemeHost.WaitFor(() => WpfSta.Invoke(() => Tools(ui).HasOverflowItems)), "No tool went to the overflow.");
        Assert.IsNotNull(ui.Host.TryFindInWindow("Filter.Mode"), "Mode left the bar.");
        Assert.IsFalse(IsOverflow(ui, "Filter.Live"), "Selection sync left the bar.");
        var sync = ui.Host.FindInWindow("Filter.Live");
        Assert.AreEqual("Selection sync", sync.Name);
        var bar = ui.Host.FindInWindow("Filter.ResultTools");
        var arrow = ui.Host.FindInWindow("ButtonOverflow");
        bool Inside() => sync.BoundingRectangle is var toggle && toggle.Width >= 20 && toggle.Left >= bar.BoundingRectangle.Left
                         && toggle.Right <= arrow.BoundingRectangle.Left + 1 && toggle.Right <= bar.BoundingRectangle.Right;
        Assert.IsTrue(ThemeHost.WaitForUi(Inside),
            $"The sync toggle {sync.BoundingRectangle} is not fully inside the bar (bar {bar.BoundingRectangle}, arrow {arrow.BoundingRectangle}).");
        Assert.IsTrue(IsOverflow(ui, "Filter.Result.Eye"), "The last tool stayed while the bar is too narrow.");

        ui.Host.FindInWindow("ButtonOverflow").Patterns.Toggle.Pattern.Toggle();
        Assert.IsTrue(ThemeHost.WaitFor(() => WpfSta.Invoke(() => Tools(ui).IsOverflowOpen)), "The overflow did not open.");
        var popup = WpfSta.Invoke(() => (Popup)LogicalTreeHelper.GetParent(
            ((FrameworkElement)Tools(ui).Template.FindName("PART_ToolBarOverflowPanel", Tools(ui))).Parent));
        var hide = Retry.WhileNull(
            () => ui.Host.PopupRoot(popup).FindFirstDescendant(cf => cf.ByAutomationId("Filter.Result.Eye")),
            TimeSpan.FromSeconds(5), ignoreException: true).Result;
        Assert.IsNotNull(hide, "Hide is not in the overflow.");
        Assert.AreEqual("Hide", hide.Name);

        hide.Patterns.Invoke.Pattern.Invoke();

        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Actions.ExecutedPlans.Count > 0), "Hide from the overflow ran no plan.");
    }

    private static PanelUi OpenWithResult(double width, bool allCategories = false)
    {
        var ui = PanelUi.Open(width);
        ui.ShowView("ModelContext.View.Filter");
        ui.Press("Filter.Add");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Library.SelectedSource is { IsCustom: true }), "No custom filter was added.");
        WpfSta.Invoke(() =>
        {
            if (allCategories)
            {
                ui.Panel.Filter.Categories.CheckAllCommand.Execute(null);
            }
            else
            {
                ui.Panel.Filter.Categories.Items[0].IsChecked = true;
            }

            ui.Clock.Advance(FilterRunner.LiveDelay);
        });
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Runner.ResultCount > 0), "The filter found nothing.");
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindInWindow("Filter.ResultCount").Name
                == WpfSta.Invoke(() => ui.Panel.Filter.Runner.ResultCount.ToString("N0"))), "The count did not show.");
        return ui;
    }

    private static bool IsOverflow(PanelUi ui, string id)
    {
        return WpfSta.Invoke(() => Tools(ui).Items.OfType<FrameworkElement>()
            .Any(item => AutomationProperties.GetAutomationId(item) == id && ToolBar.GetIsOverflowItem(item)));
    }

    private static ToolBar Tools(PanelUi ui)
    {
        var filter = (FilterTabView)ui.View.FindName("FilterView");
        return (ToolBar)filter.FindName("ResultTools");
    }
}
