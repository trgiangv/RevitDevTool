using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Panel;

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
        var newer = fixture.Explorer.Rows.Any(row => row.Kind == TreeRowKind.LinkModel && row.Document == Mep);
        fixture.Model.ReleaseScope(0);

        Assert.AreEqual(3, fixture.Model.HeldScopes);
        Assert.IsTrue(newer);
        Assert.IsTrue(fixture.Explorer.Rows.Any(row => row.Kind == TreeRowKind.LinkModel && row.Document == Mep));
        Assert.IsFalse(fixture.Panel.IsUpdating);
    }

    [TestMethod]
    public async Task ClosingDropsSession()
    {
        using var fixture = await PanelFixture.OpenAsync();
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));
        var builds = fixture.Model.BuildCount;

        fixture.Model.RaiseDocumentClosing(Current);
        var sessions = fixture.Panel.SessionCount;
        var rows = fixture.Explorer.Rows.Count;
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
        await fixture.Explorer.ExpandAsync(fixture.Group(Current, "Ducts"));
        var builds = model.BuildCount;

        model.ActivateModel(Other.Value.CurrentModel);
        fixture.Settle();
        var otherRows = fixture.Explorer.Rows.Where(row => row.Document is not null)
            .All(row => row.Document!.Value.ModelId == "other-model");
        model.ActivateModel(Current);
        fixture.Settle();

        Assert.IsTrue(otherRows);
        CollectionAssert.AreEqual(new[] { Current }, fixture.Panel.ContextBar.TickedDocuments.ToArray());
        Assert.IsTrue(fixture.Group(Current, "Ducts").IsExpanded);
        Assert.AreEqual(2, fixture.Panel.SessionCount);
        Assert.AreEqual(builds + 1, model.BuildCount);
    }

    [TestMethod]
    public async Task DraftSurvivesModelSwitch()
    {
        var model = new FakeModel(TestModel.SmallData);
        model.AddModel(Other.Value);
        using var fixture = PanelFixture.Create(model);
        await fixture.Panel.InitializeAsync(CancellationToken.None);
        var library = fixture.Filter.Library;
        var draft = AddDraft(fixture, "Check");

        model.ActivateModel(Other.Value.CurrentModel);
        fixture.Settle();
        var away = library.SelectedSource;
        model.ActivateModel(Current);
        fixture.Settle();

        Assert.IsNull(away);
        Assert.AreEqual("New filter 1", library.SelectedSource!.Name);
        Assert.IsTrue(library.IsDirty);
        Assert.IsTrue(library.SelectedSource.IsDirty);
        Assert.AreEqual(draft, fixture.Filter.Definition().Root.Children.Single());
        library.SaveCommand.Execute(null);
        Assert.AreEqual(draft, fixture.CustomFilters.Object.Load().Single().Definition.Root.Children.Single());
    }

    [TestMethod]
    public async Task DirtySavedFilterSurvivesSwitch()
    {
        var model = new FakeModel(TestModel.SmallData);
        model.AddModel(Other.Value);
        using var fixture = PanelFixture.Create(model);
        await fixture.Panel.InitializeAsync(CancellationToken.None);
        var library = fixture.Filter.Library;
        AddDraft(fixture, "Check");
        library.SaveCommand.Execute(null);
        library.BeginRename();
        library.EditName = "Renamed";
        library.CommitRename();
        var rule = (RuleViewModel)fixture.Filter.Root.Children[0];
        rule.Value = "Other";
        var edited = fixture.Filter.Definition().Root.Children.Single();

        model.ActivateModel(Other.Value.CurrentModel);
        fixture.Settle();
        model.ActivateModel(Current);
        fixture.Settle();

        Assert.AreEqual("Renamed", library.SelectedSource!.Name);
        Assert.IsTrue(library.IsDirty);
        Assert.AreEqual(edited, fixture.Filter.Definition().Root.Children.Single());
        Assert.AreEqual("New filter 1", fixture.CustomFilters.Object.Load().Single().Name);
    }

    [TestMethod]
    public async Task ModelsKeepSeparateDrafts()
    {
        var model = new FakeModel(TestModel.SmallData);
        model.AddModel(Other.Value);
        using var fixture = PanelFixture.Create(model);
        await fixture.Panel.InitializeAsync(CancellationToken.None);
        var library = fixture.Filter.Library;
        var first = AddDraft(fixture, "Check");
        model.ActivateModel(Other.Value.CurrentModel);
        fixture.Settle();
        var second = AddDraft(fixture, "Review");

        model.ActivateModel(Current);
        fixture.Settle();
        var back = fixture.Filter.Definition().Root.Children.Single();
        model.ActivateModel(Other.Value.CurrentModel);
        fixture.Settle();

        Assert.AreEqual(first, back);
        Assert.AreEqual(second, fixture.Filter.Definition().Root.Children.Single());
        Assert.IsTrue(library.IsDirty);
    }

    [TestMethod]
    public async Task BusyHostDisablesActions()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var menu = fixture.Panel.ContextMenu;

        fixture.Model.SetBusy(true);
        fixture.Explorer.OpenMenu(fixture.Group(Current, "Ducts"));
        var isolate = menu[MenuAction.Isolate].DisabledReason;
        var zoom = menu[MenuAction.Zoom].IsEnabled;
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));
        var message = fixture.Panel.Message;
        fixture.Model.SetBusy(false);
        fixture.Settle();

        Assert.AreEqual(VisibilityText.HostBusy(FakeModel.FakeHostName), isolate);
        Assert.IsFalse(zoom);
        Assert.AreEqual(VisibilityText.HostBusy(FakeModel.FakeHostName), message);
        Assert.IsEmpty(fixture.Actions.ExecutedPlans);
        Assert.IsEmpty(await fixture.Model.GetSelectionAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task BusyTextNamesHost()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Model.SetBusy(true);

        Assert.AreEqual(FakeModel.FakeHostName + " is busy.", fixture.Panel.BusyText);
    }

    [TestMethod]
    public async Task DefinitionChangeRefreshesCatalog()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var reads = fixture.Model.CatalogReads;

        fixture.Model.RaiseParameterDefinitionsChanged(FakeModelGenerator.CurrentSource);

        Assert.AreEqual(reads + 1, fixture.Model.CatalogReads);
        Assert.IsNotEmpty(fixture.Filter.Root.Parameters);
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

        Assert.AreEqual(0, fixture.Explorer.SelectedCount);
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
        Assert.AreEqual(3, fixture.Explorer.SelectedCount);
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
        await fixture.Explorer.ExpandAsync(walls);
        var family = walls.Children![0];
        await fixture.Explorer.ExpandAsync(family);
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
    public async Task UnloadedLinkCountsNothingInView()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        fixture.Panel.ContextBar.Tick([Mep]);
        fixture.Settle();

        fixture.Model.SetLoaded(Mep, false);
        fixture.Settle();

        Assert.AreEqual(0, fixture.FindGroup(Mep, "Ducts")?.Count ?? 0);
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

        Assert.AreEqual(VisibilityText.NotLoaded, unloaded);
        Assert.IsTrue(fixture.Group(Mep, "Ducts").IsEnabled);
        Assert.AreEqual(builds, fixture.Model.BuildCount);
    }

    [TestMethod]
    public async Task JournalResolvesUniqueIds()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        var hides = fixture.Panel.Journal.LinkHides;
        Assert.IsNotEmpty(hides);
        Assert.IsTrue(hides.All(hide => hide.UniqueId.StartsWith("mep-", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task EyeReadsBackHostState()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var id = fixture.Explorer.ElementsOf([fixture.Group(Current, "Ducts")])[0].Id;

        await fixture.Actions.ExecuteAsync(new VisibilityPlan(VisibilityAction.Hide, null,
            [new ElementsStep(Current, [id])], null), CancellationToken.None);
        fixture.Model.RaiseElementsChanged(ElementDelta.DeletedOnly(FakeModelGenerator.CurrentSource, []));
        fixture.Settle();

        Assert.AreEqual(RowEye.Partly, fixture.Group(Current, "Ducts").Eye);
    }

    [TestMethod]
    public async Task ReadBackAsksVisibleRowsOnly()
    {
        using var fixture = await PanelFixture.OpenAsync(scope: ScopeKind.ActiveView);
        var explorer = fixture.Explorer;
        await explorer.ToggleExpandCommand.ExecuteAsync(explorer.Rows.First(row => row.Kind == TreeRowKind.CurrentModelHeader));
        fixture.Actions.HiddenReads.Clear();

        fixture.Model.RaiseViewActivated(new ViewInfo(Current, "view-2", "Level 2", ViewKind.Plan));
        fixture.Settle();
        var collapsed = fixture.Actions.HiddenReads.Sum(read => read.Count);
        await explorer.ToggleExpandCommand.ExecuteAsync(explorer.Rows.First(row => row.Kind == TreeRowKind.CurrentModelHeader));

        var asked = fixture.Actions.HiddenReads.SelectMany(read => read).ToHashSet();
        var inView = (await fixture.Model.CollectScopeAsync(Current, ScopeKind.ActiveView, CancellationToken.None))
            .ToHashSet();
        Assert.AreEqual(0, collapsed);
        Assert.IsTrue(asked.SetEquals(inView));
    }

    private static FilterNode AddDraft(PanelFixture fixture, string value)
    {
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Root.AddConditionCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];
        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.DisplayName == "Comments");
        rule.Value = value;
        fixture.Settle();
        return filter.Definition().Root.Children.Single();
    }
}
