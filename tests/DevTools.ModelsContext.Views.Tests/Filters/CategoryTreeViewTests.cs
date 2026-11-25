using DevTools.ModelsContext.Scope;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace DevTools.ModelsContext.Views.Tests.Filters;

[TestClass]
[DoNotParallelize]
public sealed class CategoryTreeViewTests
{
    private static readonly string[] Railings = ["Railings", "Top Rails", "Balusters"];

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void ParentCheckChecksChildren(string prefix)
    {
        using var ui = OpenBox(prefix);

        Box(ui, prefix, "Toposolid").Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => Checked(ui, prefix).SetEquals(["Toposolid", "Sub-divisions"])),
            "The parent check did not check its children.");
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void ChildUncheckShowsPartly(string prefix)
    {
        using var ui = OpenBox(prefix);
        Box(ui, prefix, "Railings").Patterns.Toggle.Pattern.Toggle();
        Expander(ui, prefix, "Railings").Patterns.Toggle.Pattern.Toggle();

        Box(ui, prefix, "Top Rails").Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitForUi(() => Box(ui, prefix, "Railings").Patterns.Toggle.Pattern.ToggleState.Value
                                                == ToggleState.Indeterminate), "The parent box is not partly checked.");
        Assert.IsTrue(Checked(ui, prefix).SetEquals(["Railings", "Balusters"]));
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void ChildSearchExpandsParent(string prefix)
    {
        using var ui = OpenBox(prefix);

        ui.Host.FindInWindow(prefix + ".CategorySearch").Patterns.Value.Pattern.SetValue("Sub-div");

        Assert.IsTrue(ThemeHost.WaitForUi(() => RowNames(ui, prefix).SequenceEqual(["Toposolid", "Sub-divisions"])),
            "The search did not list the parent and its child.");
        Assert.AreEqual(ToggleState.On, Expander(ui, prefix, "Toposolid").Patterns.Toggle.Pattern.ToggleState.Value);
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void ParentSelectChecksChildren(string prefix)
    {
        using var ui = OpenBox(prefix);

        Row(ui, prefix, "Railings").Patterns.SelectionItem.Pattern.Select();

        Assert.IsTrue(ThemeHost.WaitFor(() => Checked(ui, prefix).SetEquals(Railings)), "Selecting the parent did not check its children.");
        Assert.AreEqual(ToggleState.On, Box(ui, prefix, "Railings").Patterns.Toggle.Pattern.ToggleState.Value);
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void ParentDeselectUnchecks(string prefix)
    {
        using var ui = OpenBox(prefix);
        var row = Row(ui, prefix, "Railings");
        row.Patterns.SelectionItem.Pattern.Select();
        Assert.IsTrue(ThemeHost.WaitFor(() => Checked(ui, prefix).SetEquals(Railings)), "Selecting the parent did not check it.");

        row.Patterns.SelectionItem.Pattern.RemoveFromSelection();

        Assert.IsTrue(ThemeHost.WaitFor(() => Checked(ui, prefix).Count == 0), "Deselecting the parent left checks.");
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void PartlyClickChecksAll(string prefix)
    {
        using var ui = OpenBox(prefix);
        Box(ui, prefix, "Railings").Patterns.Toggle.Pattern.Toggle();
        Expander(ui, prefix, "Railings").Patterns.Toggle.Pattern.Toggle();
        Box(ui, prefix, "Top Rails").Patterns.Toggle.Pattern.Toggle();
        Assert.IsTrue(ThemeHost.WaitForUi(() => Box(ui, prefix, "Railings").Patterns.Toggle.Pattern.ToggleState.Value
                                                == ToggleState.Indeterminate), "The parent box is not partly checked.");

        Box(ui, prefix, "Railings").Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitFor(() => Checked(ui, prefix).SetEquals(Railings)), "A partly checked parent click did not check all.");
        Assert.IsTrue(ThemeHost.WaitForUi(() => Box(ui, prefix, "Railings").Patterns.Toggle.Pattern.ToggleState.Value
                                                == ToggleState.On), "The parent box does not read checked.");
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void HandCheckedChildrenCheckParent(string prefix)
    {
        using var ui = OpenBox(prefix);
        Expander(ui, prefix, "Railings").Patterns.Toggle.Pattern.Toggle();

        Box(ui, prefix, "Top Rails").Patterns.Toggle.Pattern.Toggle();
        Assert.IsTrue(ThemeHost.WaitForUi(() => Box(ui, prefix, "Railings").Patterns.Toggle.Pattern.ToggleState.Value
                                                == ToggleState.Indeterminate), "One child did not leave the parent partly checked.");
        Box(ui, prefix, "Balusters").Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitForUi(() => Box(ui, prefix, "Railings").Patterns.Toggle.Pattern.ToggleState.Value
                                                == ToggleState.On), "Every child checked did not check the parent.");
        Assert.IsTrue(Checked(ui, prefix).SetEquals(Railings));
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void SelectSpansParents(string prefix)
    {
        using var ui = OpenBox(prefix);

        Row(ui, prefix, "Railings").Patterns.SelectionItem.Pattern.Select();
        Row(ui, prefix, "Toposolid").Patterns.SelectionItem.Pattern.AddToSelection();

        Assert.IsTrue(ThemeHost.WaitFor(() => Checked(ui, prefix).SetEquals([.. Railings, "Toposolid", "Sub-divisions"])),
            "Select and add did not check both parents with their children.");
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void ChildSelectLeavesParentPartly(string prefix)
    {
        using var ui = OpenBox(prefix);
        Row(ui, prefix, "Railings").Patterns.SelectionItem.Pattern.Select();
        Expander(ui, prefix, "Railings").Patterns.Toggle.Pattern.Toggle();

        Row(ui, prefix, "Top Rails").Patterns.SelectionItem.Pattern.Select();

        Assert.IsTrue(ThemeHost.WaitFor(() => Checked(ui, prefix).SetEquals(["Top Rails"])), "Selecting a child did not check only it: " + string.Join(", ", Checked(ui, prefix)));
        Assert.AreEqual(ToggleState.Indeterminate, Box(ui, prefix, "Railings").Patterns.Toggle.Pattern.ToggleState.Value);
    }
    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void CollapseKeepsChildChecks(string prefix)
    {
        using var ui = OpenBox(prefix);
        Box(ui, prefix, "Railings").Patterns.Toggle.Pattern.Toggle();
        var expander = Expander(ui, prefix, "Railings");

        expander.Patterns.Toggle.Pattern.Toggle();
        Assert.IsTrue(ThemeHost.WaitForUi(() => RowNames(ui, prefix).Contains("Top Rails")), "Expand did not list the children.");
        expander.Patterns.Toggle.Pattern.Toggle();

        Assert.IsTrue(ThemeHost.WaitForUi(() => !RowNames(ui, prefix).Contains("Top Rails")), "Collapse left the children.");
        Assert.IsTrue(Checked(ui, prefix).SetEquals(["Railings", "Top Rails", "Balusters"]), "Collapse changed the checks.");
    }

    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void CheckDuringExpandHolds(string prefix)
    {
        using var ui = OpenBox(prefix);
        ui.Press(prefix + ".CategoryAll");
        Assert.IsTrue(ThemeHost.WaitForUi(() => RowNames(ui, prefix).Contains("Roofs")), "Roofs is not listed.");

        WpfSta.Invoke(() =>
        {
            var picker = Picker(ui, prefix);
            picker.Items.First(item => item.Name == "Toposolid").IsExpanded = true;
            picker.Items.First(item => item.Name == "Roofs").IsChecked = false;
        });

        Assert.IsTrue(ThemeHost.WaitForUi(() => RowNames(ui, prefix).Contains("Sub-divisions")), "Expand did not list the child.");
        Assert.IsFalse(Checked(ui, prefix).Contains("Roofs"), "The list checked Roofs again.");
    }
    [TestMethod]
    [DataRow("Explorer")]
    [DataRow("Filter")]
    public void ParentReselectKeepsChildren(string prefix)
    {
        using var ui = OpenBox(prefix);
        var row = Row(ui, prefix, "Railings");
        row.Patterns.SelectionItem.Pattern.Select();
        Assert.IsTrue(ThemeHost.WaitFor(() => Checked(ui, prefix).SetEquals(Railings)), "Selecting the parent did not check it.");

        row.Patterns.SelectionItem.Pattern.Select();

        Assert.IsTrue(ThemeHost.WaitFor(() => Checked(ui, prefix).SetEquals(Railings)), "Selecting the parent again dropped its children.");
    }
    private static PanelUi OpenBox(string prefix)
    {
        var ui = PanelUi.Open(subcategories: true);
        if (prefix == "Filter")
        {
            ui.ShowView("ModelContext.View.Filter");
            ui.Press("Filter.Add");
            Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Library.SelectedSource is { IsCustom: true }),
                "No custom filter was added.");
        }

        ui.Host.FindInWindow(prefix + ".Categories").Patterns.Toggle.Pattern.Toggle();
        ui.Press(prefix + ".CategoryNone");
        Assert.IsTrue(ThemeHost.WaitFor(() => Checked(ui, prefix).Count == 0), "None left checks.");
        return ui;
    }

    private static CategoryPickerViewModel Picker(PanelUi ui, string prefix)
    {
        return prefix == "Filter" ? ui.Panel.Filter.Categories : ui.Panel.Explorer.Categories.Picker;
    }

    private static HashSet<string> Checked(PanelUi ui, string prefix)
    {
        return WpfSta.Invoke(() => Picker(ui, prefix).Items.Where(item => item.IsChecked).Select(item => item.Name).ToHashSet());
    }

    private static List<string> RowNames(PanelUi ui, string prefix)
    {
        return ui.Host.FindAllInWindow(prefix + ".Category.Row").Select(row => row.Name).ToList();
    }

    private static AutomationElement Row(PanelUi ui, string prefix, string name)
    {
        return Named(ui, prefix + ".Category.Row", name);
    }

    private static AutomationElement Box(PanelUi ui, string prefix, string name)
    {
        return Named(ui, prefix + ".Category.Item", name);
    }

    private static AutomationElement Expander(PanelUi ui, string prefix, string name)
    {
        return Named(ui, prefix + ".Category.Expand", name);
    }

    private static AutomationElement Named(PanelUi ui, string automationId, string name)
    {
        AutomationElement? found = null;
        Assert.IsTrue(ThemeHost.WaitForUi(() =>
                (found = ui.Host.FindAllInWindow(automationId).FirstOrDefault(element => element.Name == name)) is not null),
            $"{automationId} '{name}' was not found.");
        return found!;
    }
}
