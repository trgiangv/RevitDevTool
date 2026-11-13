using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class LifecycleTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;

    private static readonly Lazy<FakeModelData> Other = new(() => FakeModelGenerator.Generate(new FakeModelOptions
    {
        ModelId = "other-model",
        CurrentElements = 3_000,
        LinkElements = 2_000
    }));

    [TestMethod]
    public async Task StaleResultIsDropped()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var bar = fixture.Panel.ContextBar;

        fixture.Model.HoldScopes();
        bar.Scope = ScopeKind.ActiveView;
        bar.Scope = ScopeKind.WholeModel;
        var walls = fixture.Group(Current, "Walls").Count;
        fixture.Model.ReleaseScopes();

        Assert.AreEqual(walls, fixture.Group(Current, "Walls").Count);
        Assert.IsFalse(fixture.Panel.IsUpdating);
    }

    [TestMethod]
    public async Task OldJobFinishingLastIsDropped()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var bar = fixture.Panel.ContextBar;

        fixture.Model.HoldScopes();
        bar.Scope = ScopeKind.ActiveView;
        bar.Tick([Mep]);
        fixture.Model.ReleaseScope(1);
        fixture.Model.ReleaseScope(2);
        var newer = fixture.Tree.Rows.Any(row => row.Kind == TreeRowKind.LinkModel && row.Document == Mep);
        fixture.Model.ReleaseScope(0);

        Assert.AreEqual(3, fixture.Model.HeldScopes);
        Assert.IsTrue(newer);
        Assert.IsTrue(fixture.Tree.Rows.Any(row => row.Kind == TreeRowKind.LinkModel && row.Document == Mep));
        Assert.IsFalse(fixture.Panel.IsUpdating);
    }

    [TestMethod]
    public async Task ClosingDropsSession()
    {
        using var fixture = await PanelFixture.OpenAsync();
        await fixture.Tree.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));
        var builds = fixture.Model.BuildCount;

        fixture.Model.RaiseDocumentClosing(Current);
        var sessions = fixture.Panel.SessionCount;
        var rows = fixture.Tree.Rows.Count;
        fixture.Settle();

        Assert.AreEqual(0, sessions);
        Assert.AreEqual(0, rows);
        Assert.AreEqual(Current, fixture.Panel.ActiveModel);
        Assert.AreEqual(0, fixture.Panel.Journal.ChangeCount);
        Assert.AreEqual(builds + 1, fixture.Model.BuildCount);
    }

    [TestMethod]
    public async Task SharedLinkDropsWithLastUser()
    {
        var model = new FakeModel(TestModel.SmallData);
        model.AddModel(Other.Value);
        var store = new SnapshotStore(model);
        store.SetDocuments(TestModel.SmallData.Documents);
        store.SetDocuments(Other.Value.Documents);
        await store.GetAsync(FakeModelGenerator.ArchFirst, null, CancellationToken.None);
        var users = store.UsersOf(FakeModelGenerator.ArchSource);

        store.DropModel(Current.ModelId);
        var kept = store.Holds(FakeModelGenerator.ArchSource);
        store.DropModel(Other.Value.CurrentModel.ModelId);

        Assert.AreEqual(2, users);
        Assert.IsTrue(kept);
        Assert.IsFalse(store.Holds(FakeModelGenerator.ArchSource));
    }

    [TestMethod]
    public async Task ModelSwitchParksSession()
    {
        var model = new FakeModel(TestModel.SmallData);
        model.AddModel(Other.Value);
        using var fixture = PanelFixture.Create(model);
        await fixture.Panel.InitializeAsync(CancellationToken.None);
        fixture.Panel.ContextBar.Tick([Mep]);
        await fixture.Tree.ExpandAsync(fixture.Group(Current, "Ducts"));
        var builds = model.BuildCount;

        model.ActivateModel(Other.Value.CurrentModel);
        fixture.Settle();
        var otherRows = fixture.Tree.Rows.Where(row => row.Document is not null)
            .All(row => row.Document!.Value.ModelId == "other-model");
        model.ActivateModel(Current);
        fixture.Settle();

        Assert.IsTrue(otherRows);
        CollectionAssert.AreEquivalent(new[] { Current, Mep }, fixture.Panel.ContextBar.TickedDocuments.ToArray());
        Assert.IsTrue(fixture.Group(Current, "Ducts").IsExpanded);
        Assert.AreEqual(2, fixture.Panel.SessionCount);
        Assert.AreEqual(builds + 1, model.BuildCount);
    }

    [TestMethod]
    public async Task BusyHostDisablesActions()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var menu = fixture.Panel.ContextMenu;

        fixture.Model.SetBusy(true);
        fixture.Tree.OpenMenu(fixture.Group(Current, "Ducts"));
        var isolate = menu[MenuAction.Isolate].DisabledReason;
        var zoom = menu[MenuAction.Zoom].IsEnabled;
        await fixture.Tree.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));
        var message = fixture.Tree.Message;
        fixture.Model.SetBusy(false);
        fixture.Settle();

        Assert.AreEqual(VisibilityReasons.HostBusy(FakeModel.FakeHostName), isolate);
        Assert.IsFalse(zoom);
        Assert.AreEqual(VisibilityReasons.HostBusy(FakeModel.FakeHostName), message);
        Assert.IsEmpty(fixture.Model.ExecutedPlans);
        Assert.IsEmpty(await fixture.Model.GetSelectionAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task BusyTextNamesHost()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Model.SetBusy(true);

        Assert.AreEqual(FakeModel.FakeHostName + " is busy.", fixture.Panel.BusyText);
        StringAssert.EndsWith(fixture.Panel.ContextMenu[MenuAction.SelectInHost].Text, FakeModel.FakeHostName);
    }

    [TestMethod]
    public async Task DefinitionChangeRefreshesCatalog()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var reads = fixture.Model.CatalogReads;

        fixture.Model.RaiseParameterDefinitionsChanged(FakeModelGenerator.CurrentSource);

        Assert.AreEqual(reads + 1, fixture.Model.CatalogReads);
        Assert.IsNotEmpty(fixture.Filter.Parameters);
    }

    [TestMethod]
    public async Task HiddenPanePausesUpdates()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var builds = fixture.Model.BuildCount;
        var reads = fixture.Model.SelectionReads;
        var id = fixture.Model.Data.SourceOf(Current).Elements[0].Id;

        fixture.Panel.IsPaneVisible = false;
        fixture.Model.RaiseSelectionChanged(fixture.Elements(Current, 3));
        fixture.Model.RaiseElementsChanged(ElementDelta.DeletedOnly(FakeModelGenerator.CurrentSource, [id]));
        fixture.Settle();

        Assert.AreEqual(0, fixture.Tree.SelectedCount);
        Assert.AreEqual(reads, fixture.Model.SelectionReads);
        Assert.AreEqual(builds, fixture.Model.BuildCount);
    }

    [TestMethod]
    public async Task ShownPaneResyncsOnce()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var id = fixture.Model.Data.SourceOf(Current).Elements[0].Id;
        fixture.Panel.IsPaneVisible = false;
        fixture.Model.RaiseSelectionChanged(fixture.Elements(Current, 2));
        fixture.Model.RaiseElementsChanged(ElementDelta.DeletedOnly(FakeModelGenerator.CurrentSource, [id]));
        fixture.Model.RaiseSelectionChanged(fixture.Elements(Current, 3));
        fixture.Settle();
        var reads = fixture.Model.SelectionReads;
        var documents = fixture.Model.DocumentReads;
        var builds = fixture.Model.BuildCount;

        fixture.Panel.IsPaneVisible = true;
        fixture.Settle();

        Assert.AreEqual(reads + 1, fixture.Model.SelectionReads);
        Assert.AreEqual(documents + 1, fixture.Model.DocumentReads);
        Assert.AreEqual(builds + 1, fixture.Model.BuildCount);
        Assert.AreEqual(3, fixture.Tree.SelectedCount);
    }

    [TestMethod]
    public async Task FirstPaintShowsQuickCounts()
    {
        var model = new FakeModel(TestModel.SmallData);
        var source = model.Data.SourceOf(Current);
        var wallTypes = source.Types.Where(type => type.GroupId == TestModel.Walls).Select(type => type.TypeId)
            .ToHashSet();
        var expected = source.Elements.Count(element => wallTypes.Contains(element.TypeId));
        model.HoldSnapshots();
        using var fixture = PanelFixture.Create(model);

        var initialize = fixture.Panel.InitializeAsync(CancellationToken.None);
        var quick = fixture.FindGroup(Current, "Walls");
        var pending = initialize.IsCompleted;
        model.ReleaseSnapshots();
        await initialize;

        Assert.IsFalse(pending);
        Assert.IsNotNull(quick);
        Assert.AreEqual(expected, quick.Count);
        Assert.IsFalse(quick.HasChildren);
        Assert.IsTrue(fixture.Group(Current, "Walls").HasChildren);
        Assert.AreEqual(expected, fixture.Group(Current, "Walls").Count);
    }

    [TestMethod]
    public async Task BranchesSurviveRebuild()
    {
        var model = new FakeModel(TestModel.SmallData) { ReverseRebuilds = true };
        using var fixture = PanelFixture.Create(model);
        await fixture.Panel.InitializeAsync(CancellationToken.None);
        var walls = fixture.Group(Current, "Walls");
        await fixture.Tree.ExpandAsync(walls);
        var family = walls.Children![0];
        await fixture.Tree.ExpandAsync(family);
        var ids = model.Data.SourceOf(Current).Elements.Take(DeltaApplier.DirtyThreshold + 1)
            .Select(element => element.Id).ToList();

        model.RaiseElementsChanged(ElementDelta.DeletedOnly(FakeModelGenerator.CurrentSource, ids));
        fixture.Settle();

        var rebuilt = fixture.Group(Current, "Walls");
        Assert.AreEqual(2, model.BuildCount);
        Assert.AreNotSame(walls, rebuilt);
        Assert.IsTrue(rebuilt.IsExpanded);
        Assert.IsTrue(rebuilt.Children!.Single(row => row.Text == family.Text).IsExpanded);
    }

    [TestMethod]
    public async Task UnloadedLinkRowsDisable()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        var builds = fixture.Model.BuildCount;

        fixture.Model.SetLoaded(Mep, false);
        fixture.Settle();
        var unloaded = fixture.Group(Mep, "Ducts").DisabledReason;
        fixture.Model.SetLoaded(Mep, true);
        fixture.Settle();

        Assert.AreEqual(VisibilityReasons.NotLoaded, unloaded);
        Assert.IsTrue(fixture.Group(Mep, "Ducts").IsEnabled);
        Assert.AreEqual(builds, fixture.Model.BuildCount);
    }

    [TestMethod]
    public async Task JournalResolvesUniqueIds()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);

        await fixture.Tree.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        var entries = fixture.Panel.Journal.LinkEntries;
        Assert.IsNotEmpty(entries);
        Assert.IsTrue(entries.All(entry => entry.UniqueId.StartsWith("mep-", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task EyeReadsBackHostState()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var id = fixture.Tree.ElementsOf([fixture.Group(Current, "Ducts")])[0].Id;

        await fixture.Model.ExecuteAsync(new VisibilityPlan(VisibilityAction.Hide, null,
            [new ElementsStep(Current, [id])], null), CancellationToken.None);
        await fixture.Panel.RefreshAsync();

        Assert.AreEqual(RowEye.Partly, fixture.Group(Current, "Ducts").Eye);
    }
}
