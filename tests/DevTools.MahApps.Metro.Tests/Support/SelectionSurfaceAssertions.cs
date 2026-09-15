using System.Collections;
using System.Windows.Controls;

namespace DevTools.MetroFork.Tests.Support;

public static class SelectionSurfaceAssertions
{
    private static readonly string[] AllTargets = ["Monitor", "File", "Http"];

    public static void AssertAll(
        MultiSelectionComboBoxFixture fixture,
        string[] expected,
        string context,
        bool dropdownOpen = false)
    {
        AssertSurface(fixture.ViewModel.SelectedLogTargets, expected, context, "VM SelectedLogTargets");
        AssertSurface(fixture.ComboBox.SelectedItems!, expected, context, "combo.SelectedItems");

        var presenter = fixture.RequireSelectedItemsPresenter();
        AssertSurface(presenter.Items, expected, context, "PART_SelectedItemsPresenter.Items");

        if (dropdownOpen)
            AssertPopupSelection(fixture, expected, context);
    }

    public static void AssertSurface(IList actual, string[] expected, string context, string surface)
    {
        var snapshot = actual.Cast<object>().Select(item => item.ToString()!).ToList();
        var unique = snapshot.Distinct().ToList();

        if (snapshot.Count != unique.Count)
        {
            Assert.Fail(
                $"{context} [{surface}]: duplicate items detected. " +
                $"full=[{Format(snapshot)}], unique=[{Format(unique)}], expected=[{Format(expected)}].");
        }

        if (snapshot.Count != expected.Length)
        {
            Assert.Fail(
                $"{context} [{surface}]: expected count {expected.Length}, got {snapshot.Count}. " +
                $"actual=[{Format(snapshot)}], expected=[{Format(expected)}].");
        }

        CollectionAssert.AreEquivalent(expected, snapshot.ToArray(), $"{context} [{surface}]");
    }

    public static void AssertBag(
        IList actual,
        string[] expectedIncludingDuplicates,
        string context,
        string surface)
    {
        var snapshot = actual.Cast<object>().Select(item => item.ToString()!).ToList();
        if (snapshot.Count != expectedIncludingDuplicates.Length)
        {
            Assert.Fail(
                $"{context} [{surface}]: expected count {expectedIncludingDuplicates.Length}, got {snapshot.Count}. " +
                $"actual=[{Format(snapshot)}], expected=[{Format(expectedIncludingDuplicates)}].");
        }

        CollectionAssert.AreEqual(expectedIncludingDuplicates, snapshot.ToArray(), $"{context} [{surface}]");
    }

    public static void AssertUserBagMirrored(
        MultiSelectionComboBoxFixture fixture,
        string[] expectedBag,
        string context,
        bool dropdownOpen = false)
    {
        AssertBag(fixture.ViewModel.SelectedLogTargets, expectedBag, context, "VM SelectedLogTargets");
        AssertBag(fixture.ComboBox.SelectedItems!, expectedBag, context, "combo.SelectedItems");
        AssertBag(fixture.RequireSelectedItemsPresenter().Items, expectedBag, context, "PART_SelectedItemsPresenter.Items");

        if (dropdownOpen)
        {
            var unique = expectedBag.Distinct().ToArray();
            AssertPopupSelection(fixture, unique, context);
        }
    }

    public static void AssertPopupSelection(MultiSelectionComboBoxFixture fixture, string[] expectedSelected, string context)
    {
        var popupList = fixture.RequirePopupListBox();
        popupList.UpdateLayout();

        foreach (var item in AllTargets)
        {
            var container = WaitForContainer(popupList, item);
            var shouldBeSelected = expectedSelected.Contains(item);
            Assert.AreEqual(
                shouldBeSelected,
                container.IsSelected,
                $"{context} [PART_PopupListBox ListBoxItem.IsSelected]: '{item}' expected {shouldBeSelected}, " +
                $"popup selected=[{Format(popupList.SelectedItems.Cast<object>().Select(x => x.ToString()!).ToList())}].");
        }

        AssertSurface(popupList.SelectedItems, expectedSelected, context, "PART_PopupListBox.SelectedItems");
    }

    public static ListBoxItem WaitForContainer(ListBox listBox, object item)
    {
        for (var i = 0; i < 20; i++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromItem(item) is ListBoxItem container)
                return container;

            listBox.UpdateLayout();
            MultiSelectionComboBoxFixture.Pump(listBox.Dispatcher);
        }

        Assert.Fail($"ListBoxItem for '{item}' was not generated.");
        return null!;
    }

    public static void OpenDropdown(MultiSelectionComboBoxFixture fixture)
    {
        fixture.ComboBox.IsDropDownOpen = true;
        MultiSelectionComboBoxFixture.Pump(fixture.Window.Dispatcher);
        fixture.RequirePopupListBox().UpdateLayout();
    }

    public static void SetPopupItemSelected(MultiSelectionComboBoxFixture fixture, string item, bool selected)
    {
        var popupList = fixture.RequirePopupListBox();
        var container = WaitForContainer(popupList, item);
        container.IsSelected = selected;
        MultiSelectionComboBoxFixture.Pump(fixture.Window.Dispatcher);
    }

    private static string Format(IReadOnlyList<string> items) => string.Join(", ", items);
}
