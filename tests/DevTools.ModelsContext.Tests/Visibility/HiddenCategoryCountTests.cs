using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using static DevTools.ModelsContext.Tests.TestModel;

namespace DevTools.ModelsContext.Tests.Visibility;

[TestClass]
public sealed class HiddenCategoryCountTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;

    [TestMethod]
    public async Task HiddenCategoryKeepsCount()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var before = fixture.Group(Current, "Ducts").Count;

        await Item(fixture, "Ducts").ToggleEyeCommand.ExecuteAsync(null);
        await fixture.Panel.RefreshAsync();

        var ducts = fixture.Group(Current, "Ducts");
        Assert.AreEqual(before, ducts.Count);
        Assert.AreEqual(RowEye.HiddenByCategory, ducts.Eye);
        Assert.AreEqual(before, Item(fixture, "Ducts").Count);
    }

    [TestMethod]
    public async Task LinkHiddenCategoryKeepsCount()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        fixture.Panel.ContextBar.Tick([Mep]);
        await fixture.Panel.RefreshAsync();
        var before = fixture.Group(Mep, "Ducts").Count;

        await Item(fixture, "Ducts").ToggleEyeCommand.ExecuteAsync(null);
        await fixture.Panel.RefreshAsync();

        var ducts = fixture.Group(Mep, "Ducts");
        Assert.AreEqual(before, ducts.Count);
        Assert.AreEqual(RowEye.HiddenByCategory, ducts.Eye);
    }

    [TestMethod]
    public async Task SubcategoryKeepsCountUnderParent()
    {
        using var fixture = await PanelFixture.OpenAsync(WithToposolid(), ScopeKind.ActiveView);
        var before = fixture.FindGroup(Current, SubDivisionName)?.Count;

        await Item(fixture, "Toposolid").ToggleEyeCommand.ExecuteAsync(null);
        await fixture.Panel.RefreshAsync();

        Assert.AreEqual(before, fixture.FindGroup(Current, SubDivisionName)?.Count);
    }

    [TestMethod]
    public async Task IsolateKeepsOtherCounts()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var before = fixture.Group(Current, "Ducts").Count;
        fixture.Explorer.Categories.Picker.CheckNoneCommand.Execute(null);
        Item(fixture, "Walls").IsChecked = true;

        await fixture.Explorer.Categories.IsolateCommand.ExecuteAsync(null);
        fixture.Explorer.Categories.Picker.CheckAllCommand.Execute(null);
        await fixture.Panel.RefreshAsync();

        var ducts = fixture.Group(Current, "Ducts");
        Assert.AreEqual(before, ducts.Count);
        Assert.AreEqual(RowEye.Hidden, ducts.Eye);
    }

    [TestMethod]
    public async Task ResetKeepsCount()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var before = fixture.Group(Current, "Ducts").Count;
        await Item(fixture, "Ducts").ToggleEyeCommand.ExecuteAsync(null);
        await fixture.Panel.RefreshAsync();

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);
        await fixture.Panel.RefreshAsync();

        var ducts = fixture.Group(Current, "Ducts");
        Assert.AreEqual(before, ducts.Count);
        Assert.AreNotEqual(RowEye.Hidden, ducts.Eye);
    }

    [TestMethod]
    public async Task WholeModelHideThenActiveView()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var expected = await InViewCountAsync(fixture, Current, "Ducts");

        await Item(fixture, "Ducts").ToggleEyeCommand.ExecuteAsync(null);
        fixture.Panel.ContextBar.Scope = ScopeKind.ActiveView;
        await fixture.Panel.RefreshAsync();

        var ducts = fixture.Group(Current, "Ducts");
        Assert.AreEqual(expected, ducts.Count);
        Assert.AreEqual(RowEye.HiddenByCategory, ducts.Eye);
    }

    private static async Task<int> InViewCountAsync(PanelFixture fixture, DocumentKey document, string group)
    {
        var snapshot = fixture.Model.SnapshotOf(document);
        var index = snapshot.GroupIndexOf(snapshot.Groups.First(row => snapshot.Names[row.NameIndex] == group).GroupId);
        var ids = await fixture.Model.CollectScopeAsync(document, ScopeKind.ActiveView, CancellationToken.None);
        return ids.Count(id => snapshot.TryGetRow(id, out var row) && row.GroupIndex == index);
    }

    private static CategoryItemViewModel Item(PanelFixture fixture, string name)
    {
        return fixture.Explorer.Categories.Picker.Items.First(item => item.Name == name);
    }
}
