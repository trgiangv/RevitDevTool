using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Visibility;

[TestClass]
public sealed class HideCountTests
{
    private const long UndrawnGroup = -2009990;
    private const long UndrawnType = 6_100_000;

    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;

    [TestMethod]
    public async Task ModelHideKeepsViewCount()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var count = Header(fixture).Count;
        var categories = fixture.Explorer.Categories.Picker.Items.Count;

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(Header(fixture));
        HideInHost(fixture);
        Reload(fixture);

        Assert.AreEqual(count, Header(fixture).Count);
        Assert.AreEqual(RowEye.Hidden, Header(fixture).Eye);
        Assert.AreEqual(categories, fixture.Explorer.Categories.Picker.Items.Count);
    }

    [TestMethod]
    public async Task IsolateKeepsOtherCounts()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var walls = fixture.Group(Current, "Walls").Count;
        var kept = fixture.Explorer.ElementsOf([fixture.Group(Current, "Ducts")]).ToHashSet();
        var drawn = fixture.Explorer.ElementsOf([Header(fixture)]);

        fixture.Explorer.OpenMenu(fixture.Group(Current, "Ducts"));
        await fixture.Panel.ContextMenu[MenuAction.Isolate].Command.ExecuteAsync(null);
        fixture.Model.ViewHiddenElements.UnionWith(drawn.Where(element => !kept.Contains(element)));
        Reload(fixture);

        Assert.AreEqual(walls, fixture.Group(Current, "Walls").Count);
        Assert.AreEqual(RowEye.Hidden, fixture.Group(Current, "Walls").Eye);
    }

    [TestMethod]
    public async Task HostHiddenStaysUncounted()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var ducts = fixture.Group(Current, "Ducts");
        var count = ducts.Count;
        var ids = fixture.Explorer.ElementsOf([ducts]).Take(2).ToList();

        fixture.Model.ViewHiddenElements.UnionWith(ids);
        Reload(fixture);

        Assert.AreEqual(count - ids.Count, fixture.Group(Current, "Ducts").Count);
    }

    [TestMethod]
    public async Task StaleLoadDoesNotWin()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var count = Header(fixture).Count;
        var unseen = Header(fixture).HiddenCount;
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(Header(fixture));
        HideInHost(fixture);
        Reload(fixture);
        fixture.Model.HoldScopes();
        fixture.Model.RaiseElementsChanged(ElementDelta.DeletedOnly(FakeModelGenerator.CurrentSource, []));
        fixture.Settle();
        var held = fixture.Model.HeldScopes;

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(Header(fixture));
        fixture.Model.ViewHiddenElements.Clear();
        fixture.Model.ReleaseScopes();
        fixture.Settle();

        var header = Header(fixture);
        Assert.IsGreaterThan(0, held);
        Assert.AreEqual(count, header.Count);
        Assert.AreEqual(RowEye.Shown, header.Eye);
        Assert.IsFalse(header.IsDimmed);
        Assert.AreEqual(unseen, header.HiddenCount);
    }

    [TestMethod]
    public async Task ShowDuringLoadKeepsRows()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var count = fixture.Group(Current, "Ducts").Count;
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));
        HideInHost(fixture);
        var sizes = new List<int>();
        fixture.Explorer.Rows.CollectionChanged += (_, _) => sizes.Add(fixture.Explorer.Rows.Count);
        var gate = new TaskCompletionSource<bool>();
        fixture.Actions.NextHiddenReadGate = gate;
        fixture.Model.RaiseElementsChanged(ElementDelta.DeletedOnly(FakeModelGenerator.CurrentSource, []));
        fixture.Settle();
        var holding = fixture.Actions.NextHiddenReadGate is null;

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));
        fixture.Model.ViewHiddenElements.Clear();
        gate.SetResult(true);
        fixture.Settle();

        var ducts = fixture.Group(Current, "Ducts");
        Assert.IsTrue(holding);
        Assert.DoesNotContain(0, sizes);
        Assert.AreEqual(count, ducts.Count);
        Assert.AreEqual(RowEye.Shown, ducts.Eye);
    }

    [TestMethod]
    public async Task LinkHideAddsNoUndrawnRows()
    {
        using var fixture = await OpenWithLinkAsync(WithUndrawnLinkRows());
        var groups = LinkGroups(fixture);

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(LinkRow(fixture));
        Reload(fixture);

        Assert.AreEqual(RowEye.Hidden, LinkRow(fixture).Eye);
        Assert.IsNotEmpty(groups);
        CollectionAssert.AreEqual(groups, LinkGroups(fixture));
    }

    [TestMethod]
    public async Task ViewLinkShowOpensEye()
    {
        using var fixture = await OpenWithLinkAsync(TestModel.SmallData);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        Assert.AreEqual(RowEye.Shown, fixture.Group(Mep, "Ducts").Eye);
        Assert.AreEqual(RowEye.Shown, LinkRow(fixture).Eye);
    }

    private static TreeRow Header(PanelFixture fixture)
    {
        return fixture.Explorer.Rows.First(row => row.Kind == TreeRowKind.CurrentModelHeader);
    }

    private static TreeRow LinkRow(PanelFixture fixture)
    {
        return fixture.Explorer.Rows.First(row => row.Kind == TreeRowKind.LinkModel && row.Document == Mep);
    }

    private static List<string> LinkGroups(PanelFixture fixture)
    {
        return fixture.Explorer.Rows.Where(row => row.Kind == TreeRowKind.Group && row.Document == Mep)
            .Select(row => row.Text)
            .ToList();
    }

    private static void HideInHost(PanelFixture fixture)
    {
        var step = fixture.Actions.ExecutedPlans.Last().Steps.OfType<ElementsStep>().Single();
        fixture.Model.ViewHiddenElements.UnionWith(step.Ids.Select(id => new ElementRef(step.Document, id)));
    }

    private static void Reload(PanelFixture fixture)
    {
        fixture.Model.RaiseElementsChanged(ElementDelta.DeletedOnly(FakeModelGenerator.CurrentSource, []));
        fixture.Settle();
    }

    private static async Task<PanelFixture> OpenWithLinkAsync(FakeModelData data)
    {
        var fixture = await PanelFixture.OpenAsync(data, ScopeKind.ActiveView);
        fixture.Model.ViewLinks.Add(Mep);
        fixture.Panel.ContextBar.Check([Current, Mep]);
        await fixture.Panel.RefreshAsync();
        return fixture;
    }

    private static FakeModelData WithUndrawnLinkRows()
    {
        var data = TestModel.SmallData;
        var source = data.Sources[FakeModelGenerator.MepSource];
        var level = source.Elements[0].LevelId;
        var type = new TypeRecord(UndrawnType, UndrawnGroup, "Undrawn", "Undrawn", "Undrawn");
        var elements = new[] { 1, 4, 7 }
            .Select(index => new ElementRecord(UndrawnType + index, UndrawnType, level, 1, Draw: ViewDraw.None));
        var sources = data.Sources.ToDictionary(pair => pair.Key,
            pair => pair.Key == FakeModelGenerator.MepSource
                ? pair.Value with { Types = [.. pair.Value.Types, type], Elements = [.. pair.Value.Elements, .. elements] }
                : pair.Value);
        return data with { Sources = sources };
    }
}
