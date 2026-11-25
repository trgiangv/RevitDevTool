using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Selection;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests.Explorer;

[TestClass]
public sealed class ExplorerPickTests
{
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;

    [TestMethod]
    public async Task PickNeedsCheckedLink()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var pick = fixture.Explorer.Pick;

        Assert.AreEqual(PickController.NoLinkReason, pick.DisabledReason);
        Assert.AreEqual(PickController.NoLinkReason, pick.ToolTip);
        Assert.IsFalse(pick.PickCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task CheckedLinkEnablesPick()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Panel.ContextBar.Check([Mep]);

        var pick = fixture.Explorer.Pick;
        Assert.IsNull(pick.DisabledReason);
        Assert.AreEqual(ExplorerPick.PickTip, pick.ToolTip);
        Assert.IsTrue(pick.PickCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task NoCategoryDisablesPick()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Check([Mep]);

        fixture.Explorer.Categories.Picker.CheckNoneCommand.Execute(null);

        Assert.AreEqual(PickController.NoCategoryReason, fixture.Explorer.Pick.DisabledReason);
        Assert.IsFalse(fixture.Explorer.Pick.PickCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task PickUsesCheckedBoxes()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Check([Mep]);
        var picker = fixture.Explorer.Categories.Picker;
        picker.CheckNoneCommand.Execute(null);
        picker.Items.First(item => item.Name == "Ducts").IsChecked = true;
        fixture.Model.PickCandidates = [new ElementRef(Mep, 7)];

        await fixture.Explorer.Pick.PickCommand.ExecuteAsync(null);

        CollectionAssert.AreEqual(new[] { Mep }, fixture.Model.LastPickLinks.ToList());
        CollectionAssert.AreEqual(new[] { TestModel.Ducts }, fixture.Model.LastPickCategories.ToList());
        Assert.AreEqual("Picked 1", fixture.Explorer.Pick.Status);
    }

    [TestMethod]
    public async Task PickShowsCueUntilDone()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Check([Mep]);
        fixture.Model.PickGate = new TaskCompletionSource<bool>();

        var running = fixture.Explorer.Pick.PickCommand.ExecuteAsync(null);
        var during = fixture.Explorer.Pick.Status;
        var picking = fixture.Explorer.Pick.IsPicking;
        fixture.Model.PickGate.SetResult(true);
        await running;

        Assert.AreEqual(PickController.PickingCue, during);
        Assert.IsTrue(picking);
        Assert.AreEqual(PickController.NothingCue, fixture.Explorer.Pick.Status);
    }

    [TestMethod]
    public async Task SelectionScopeKeepsPick()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.Selection);

        fixture.Panel.ContextBar.Check([Mep]);

        Assert.IsNotEmpty(fixture.Explorer.Categories.Picker.Items);
        Assert.IsNull(fixture.Explorer.Pick.DisabledReason);
    }
}
