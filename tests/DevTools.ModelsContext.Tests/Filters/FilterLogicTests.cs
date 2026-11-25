using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Tests.Shared;
using System.Globalization;
using System.Text.Json;

namespace DevTools.ModelsContext.Tests.Filters;

[TestClass]
public sealed class FilterLogicTests
{
    private static readonly SourceId Arch = FakeModelGenerator.ArchSource;
    private static readonly SourceId Mep = FakeModelGenerator.MepSource;

    [TestMethod]
    public async Task ConnectorClickSwitchesGroup()
    {
        using var fixture = await OpenAllAsync();
        var root = fixture.Filter.Root;
        root.AddConditionCommand.Execute(null);
        root.AddConditionCommand.Execute(null);
        var first = root.Children[0];
        var second = root.Children[1];

        second.ToggleConnectorCommand.Execute(null);

        Assert.IsFalse(first.ShowsConnector);
        Assert.IsTrue(second.ShowsConnector);
        Assert.AreEqual(SetMatch.Any, root.Match);
        Assert.AreEqual("OR", second.ConnectorText);
        Assert.IsTrue(fixture.Filter.Library.IsDirty);
    }

    [TestMethod]
    public void AllParentDropsExcludedGroup()
    {
        var scoped = FilterSet.All(Rule(FakeParameters.Comments, FilterOperator.NotEmpty)).In(FilterScope.Of([Arch]));
        var root = FilterSet.All(Rule(FakeParameters.Mark, FilterOperator.Defined), scoped);
        var known = new HashSet<SourceId> { Arch, Mep };

        Assert.IsNull(FilterScopes.ForDocument(root, Mep, known));
        Assert.HasCount(2, FilterScopes.ForDocument(root, Arch, known)!.Children);
    }

    [TestMethod]
    public void AnyParentSkipsExcludedGroup()
    {
        var scoped = FilterSet.All(Rule(FakeParameters.Comments, FilterOperator.NotEmpty)).In(FilterScope.Of([Arch]));
        var root = FilterSet.Any(Rule(FakeParameters.Mark, FilterOperator.Defined), scoped);

        var pruned = FilterScopes.ForDocument(root, Mep, new HashSet<SourceId> { Arch, Mep });

        Assert.HasCount(1, pruned!.Children);
        Assert.IsInstanceOfType<FilterRule>(pruned.Children[0]);
    }

    [TestMethod]
    public async Task UnresolvedGroupMatchesNothing()
    {
        using var fixture = await OpenAllAsync();
        var filter = fixture.Filter;
        var group = AddGroup(filter.Root);
        Condition(group, FakeParameters.Comments, FilterOperator.NotEmpty);

        group.SetDocumentScope([new SourceId("gone.rvt")]);
        fixture.Settle();

        Assert.IsTrue(group.IsUnresolved);
        StringAssert.Contains(group.Warning, "matches nothing");
        Assert.AreEqual(0, filter.Runner.ResultCount);
    }

    [TestMethod]
    public async Task ScopeListsOnlyParentItems()
    {
        using var fixture = await OpenAllAsync();
        var outer = AddGroup(fixture.Filter.Root);
        outer.SetDocumentScope([Arch]);
        var inner = AddGroup(outer);

        CollectionAssert.AreEqual(new[] { "Arch.rvt" }, inner.DocumentOptions.Select(option => option.Name).ToArray());
        Assert.IsGreaterThan(1, outer.DocumentOptions.Count);
        Assert.IsNull(inner.Warning);
    }

    [TestMethod]
    public async Task OutsideValueShowsWarning()
    {
        using var fixture = await OpenAllAsync();
        var outer = AddGroup(fixture.Filter.Root);
        var inner = AddGroup(outer);
        inner.SetDocumentScope([Mep]);

        outer.SetDocumentScope([Arch]);

        var option = inner.DocumentOptions.Single(item => item.Name == "MEP-Mech.rvt");
        Assert.IsTrue(option.IsChecked);
        Assert.IsTrue(option.IsOutsideParent);
        StringAssert.Contains(inner.Warning, "MEP-Mech.rvt");
        Assert.IsFalse(inner.IsUnresolved);
    }

    [TestMethod]
    public async Task ScopeOptionCheckNarrowsGroup()
    {
        using var fixture = await OpenAllAsync();
        var group = AddGroup(fixture.Filter.Root);

        group.CategoryOptions.Single(option => option.Name == "Ducts").IsChecked = true;

        CollectionAssert.AreEqual(new[] { TestModel.Ducts }, group.ScopeCategories.ToArray());
        CollectionAssert.AreEqual(new[] { TestModel.Ducts }, group.Effective.Categories!.ToArray());
    }

    [TestMethod]
    public async Task PickerFollowsGroupCategories()
    {
        using var fixture = await OpenAllAsync();
        var group = AddGroup(fixture.Filter.Root);
        group.SetCategoryScope([TestModel.Ducts]);
        group.AddConditionCommand.Execute(null);
        var condition = (RuleViewModel)group.Children[0];

        var keys = condition.Parameters.Select(parameter => parameter.Key).ToList();

        CollectionAssert.Contains(keys, FakeParameters.Insulation);
        CollectionAssert.DoesNotContain(keys, FakeParameters.SwingAngle);
        CollectionAssert.Contains(fixture.Filter.Root.Parameters.Select(parameter => parameter.Key).ToList(),
            FakeParameters.SwingAngle);
    }

    [TestMethod]
    public async Task ModelsFollowGroupDocuments()
    {
        using var fixture = await OpenAllAsync();
        var group = AddGroup(fixture.Filter.Root);
        group.SetDocumentScope([Mep]);
        group.AddConditionCommand.Execute(null);
        var condition = (RuleViewModel)group.Children[0];

        Assert.IsTrue(condition.Parameters.All(parameter => parameter.Models.Count == 1));
        CollectionAssert.DoesNotContain(condition.Parameters.Select(parameter => parameter.Key).ToList(),
            FakeParameters.Function);
    }

    [TestMethod]
    public async Task ValuesFollowGroupScope()
    {
        using var fixture = await OpenAllAsync();
        var group = AddGroup(fixture.Filter.Root);
        group.SetDocumentScope([Mep]);
        group.SetCategoryScope([TestModel.Ducts]);
        var condition = Condition(group, FakeParameters.SystemType, FilterOperator.Equal, "Supply");

        await condition.LoadValuesCommand.ExecuteAsync(null);

        var snapshot = TestModel.Snapshot(FakeModelGenerator.Mep);
        var ducts = snapshot.Rows.Count(row => snapshot.GroupName(row.GroupIndex) == "Ducts");
        Assert.IsGreaterThan(0, ducts);
        Assert.AreEqual(ducts, condition.Values.Sum(value => value.Count));
    }

    [TestMethod]
    public async Task GroupCannotMoveIntoChild()
    {
        using var fixture = await OpenAllAsync();
        var outer = AddGroup(fixture.Filter.Root);
        var inner = AddGroup(outer);

        Assert.IsFalse(fixture.Filter.MoveTo(outer, inner, 0));
        Assert.IsFalse(outer.CanMoveTo(outer));
        Assert.AreSame(fixture.Filter.Root, outer.Group);
        Assert.AreSame(outer, inner.Group);
    }

    [TestMethod]
    public async Task MoveDownSwapsSiblings()
    {
        using var fixture = await OpenAllAsync();
        var root = fixture.Filter.Root;
        root.AddConditionCommand.Execute(null);
        root.AddConditionCommand.Execute(null);
        var first = root.Children[0];

        first.MoveDownCommand.Execute(null);

        Assert.AreSame(first, root.Children[1]);
        Assert.IsTrue(first.ShowsConnector);
        Assert.IsFalse(first.MoveDownCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task MoveIntoOtherGroup()
    {
        using var fixture = await OpenAllAsync();
        var root = fixture.Filter.Root;
        var condition = Condition(root, FakeParameters.Comments, FilterOperator.NotEmpty);
        var group = AddGroup(root);
        group.SetCategoryScope([TestModel.Ducts]);

        Assert.IsTrue(fixture.Filter.MoveTo(condition, group, 0));

        Assert.AreSame(group, condition.Group);
        Assert.HasCount(1, root.Children);
        CollectionAssert.DoesNotContain(condition.Parameters.Select(parameter => parameter.Key).ToList(),
            FakeParameters.SwingAngle);
    }

    [TestMethod]
    public void StoreKeepsScopeWithoutNot()
    {
        var root = FilterSet.Any(
            FilterSet.All(Rule(FakeParameters.FireRating, FilterOperator.Equal, "2h"))
                .In(FilterScope.Of([Arch], [TestModel.Walls])),
            Rule(FakeParameters.Insulation, FilterOperator.Undefined));
        var filters = new[] { new CustomFilter("Scoped", new FilterDefinition([TestModel.Walls], root)) };

        var json = JsonSerializer.Serialize(StoredFilters.Write(filters));
        var loaded = StoredFilters.Read(JsonSerializer.Deserialize<ModelContextFilters>(json)).Single();

        StringAssert.Contains(json, "\"scope\"");
        StringAssert.Contains(json, "\"Undefined\"");
        Assert.IsFalse(json.Contains("\"not\""), json);
        var group = (FilterSet)loaded.Definition.Root.Children[0];
        Assert.AreEqual(SetMatch.Any, loaded.Definition.Root.Match);
        CollectionAssert.AreEqual(new[] { Arch }, group.Scope!.Documents.ToArray());
        CollectionAssert.AreEqual(new[] { TestModel.Walls }, group.Scope.Categories.ToArray());
        Assert.AreEqual(FilterOperator.Undefined, ((FilterRule)loaded.Definition.Root.Children[1]).Operator);
    }

    [TestMethod]
    public void StoreKeepsPickedNumber()
    {
        var rule = new FilterRule(FakeParameters.Width, FilterOperator.Equal, "2°", Picked: new NumberRange(0.0349, 0.0350));
        var filters = new[] { new CustomFilter("Picked", new FilterDefinition([TestModel.Walls], FilterSet.All(rule))) };

        var json = JsonSerializer.Serialize(StoredFilters.Write(filters));
        var loaded = (FilterRule)StoredFilters.Read(JsonSerializer.Deserialize<ModelContextFilters>(json)).Single()
            .Definition.Root.Children[0];

        Assert.AreEqual(rule, loaded);
    }

    [TestMethod]
    [DataRow(FilterOperator.Undefined, "undefined")]
    [DataRow(FilterOperator.Defined, "defined")]
    [DataRow(FilterOperator.Empty, "empty")]
    [DataRow(FilterOperator.NotEmpty, "not empty")]
    public void OperatorLabelsUseReviztoWords(FilterOperator op, string label)
    {
        Assert.AreEqual(label, EnumText.Of(op));
    }

    [TestMethod]
    public async Task ExteriorWallsNotRatedOneOrTwo()
    {
        var root = FilterSet.All(
            Rule(FakeParameters.Function, FilterOperator.Equal, "Exterior"),
            Rule(FakeParameters.FireRating, FilterOperator.NotEqual, "1h"),
            Rule(FakeParameters.FireRating, FilterOperator.NotEqual, "2h"));

        var expected = Expected(document => Matching(document, null, row =>
            Get(document, row, FakeParameters.Function) == "Exterior"
            && Get(document, row, FakeParameters.FireRating) is { } rating && rating != "1h" && rating != "2h"));

        Assert.IsGreaterThan(0, expected);
        Assert.AreEqual(expected, await CountAsync(new FilterDefinition([], root)));
    }

    [TestMethod]
    public async Task BlankCommentsOnUpperLevels()
    {
        var root = FilterSet.All(
            FilterSet.Any(Rule(FakeParameters.Comments, FilterOperator.Undefined),
                Rule(FakeParameters.Comments, FilterOperator.Empty)),
            FilterSet.Any(Rule(FakeParameters.Level, FilterOperator.Equal, "Level 2"),
                Rule(FakeParameters.Level, FilterOperator.Equal, "Level 3")));

        var expected = Expected(document => Matching(document, null, row =>
            Get(document, row, FakeParameters.Comments) is null or ""
            && Get(document, row, FakeParameters.Level) is "Level 2" or "Level 3"));

        Assert.IsGreaterThan(0, expected);
        Assert.AreEqual(expected, await CountAsync(new FilterDefinition([], root)));
    }

    [TestMethod]
    public async Task SupplyWideOrUninsulatedDucts()
    {
        var root = FilterSet.All(
            Rule(FakeParameters.Level, FilterOperator.Equal, "Level 2"),
            FilterSet.Any(
                FilterSet.All(Rule(FakeParameters.SystemType, FilterOperator.Equal, "Supply"),
                    Rule(FakeParameters.Width, FilterOperator.Greater, "400")),
                Rule(FakeParameters.Insulation, FilterOperator.Undefined)));

        var expected = Expected(document => Matching(document, TestModel.Ducts, row =>
            Get(document, row, FakeParameters.Level) == "Level 2"
            && ((Get(document, row, FakeParameters.SystemType) == "Supply"
                 && double.Parse(Get(document, row, FakeParameters.Width)!, CultureInfo.InvariantCulture) > 400)
                || Get(document, row, FakeParameters.Insulation) is null)));

        Assert.IsGreaterThan(0, expected);
        Assert.AreEqual(expected, await CountAsync(new FilterDefinition([TestModel.Ducts], root)));
    }

    [TestMethod]
    public async Task ScopedGroupsPickPerFile()
    {
        var root = FilterSet.Any(
            FilterSet.All(Rule(FakeParameters.FireRating, FilterOperator.Equal, "2h"))
                .In(FilterScope.Of([Arch], [TestModel.Walls])),
            FilterSet.All(Rule(FakeParameters.Insulation, FilterOperator.Undefined))
                .In(FilterScope.Of([Mep], [TestModel.Ducts])));

        var expected = Expected(document =>
            document.Source == Arch
                ? Matching(document, TestModel.Walls, row => Get(document, row, FakeParameters.FireRating) == "2h")
                : document.Source == Mep
                    ? Matching(document, TestModel.Ducts, row => Get(document, row, FakeParameters.Insulation) is null)
                    : 0);

        var current = TestModel.SmallData.Document(FakeModelGenerator.Current);
        Assert.IsGreaterThan(0, Matching(current, TestModel.Walls, row => Get(current, row, FakeParameters.FireRating) == "2h"));
        Assert.IsGreaterThan(0, expected);
        Assert.AreEqual(expected, await CountAsync(new FilterDefinition([], root)));
    }

    [TestMethod]
    public async Task PanelPrunesScopedGroups()
    {
        using var fixture = await OpenAllAsync();
        var filter = fixture.Filter;
        filter.Root.Match = SetMatch.Any;
        var walls = AddGroup(filter.Root);
        walls.SetDocumentScope([Arch]);
        walls.SetCategoryScope([TestModel.Walls]);
        Condition(walls, FakeParameters.FireRating, FilterOperator.Equal, "2h");

        fixture.Settle();

        var archKeys = new[] { FakeModelGenerator.ArchFirst, FakeModelGenerator.ArchSecond };
        Assert.IsGreaterThan(0, filter.Runner.ResultCount);
        Assert.IsTrue(filter.Runner.Result.All(element => archKeys.Contains(element.Document)));
        Assert.IsTrue(archKeys.All(key => filter.Runner.Result.Any(element => element.Document == key)));
    }

    private static FilterRule Rule(ParameterKey key, FilterOperator op, string value = "")
    {
        return new FilterRule(key, op, value);
    }

    private static string? Get(DocumentInfo document, ElementRow row, ParameterKey key)
    {
        var snapshot = Snapshot(document);
        return FakeParameters.TryGet(snapshot, row, key, TestModel.SmallData.Levels, out var value) ? value : null;
    }

    private static DocumentSnapshot Snapshot(DocumentInfo document)
    {
        return Snapshots.GetOrAdd(document.Key, TestModel.Snapshot);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<DocumentKey, DocumentSnapshot> Snapshots = new();

    private static int Matching(DocumentInfo document, long? category, Func<ElementRow, bool> match)
    {
        var snapshot = Snapshot(document);
        return snapshot.Rows.Count(row =>
            (category is null || snapshot.Groups[row.GroupIndex].GroupId == category) && match(row));
    }

    private static int Expected(Func<DocumentInfo, int> perDocument)
    {
        return TestModel.SmallData.Documents.Sum(perDocument);
    }

    private static async Task<int> CountAsync(FilterDefinition definition)
    {
        var data = TestModel.SmallData;
        var model = new FakeModel(data);
        var known = data.Documents.Select(document => document.Source).ToHashSet();
        var count = 0;
        foreach (var document in data.Documents)
        {
            if (FilterScopes.ForDocument(definition.Root, document.Source, known) is not { } root)
            {
                continue;
            }

            var ids = await model.EvaluateAsync(document.Key, definition with { Root = root }, null,
                CancellationToken.None);
            count += ids.Count;
        }

        return count;
    }

    private static RuleSetViewModel AddGroup(RuleSetViewModel parent)
    {
        parent.AddGroupCommand.Execute(null);
        return (RuleSetViewModel)parent.Children[parent.Children.Count - 1];
    }

    private static RuleViewModel Condition(RuleSetViewModel group, ParameterKey key, FilterOperator op, string value = "")
    {
        group.AddConditionCommand.Execute(null);
        var condition = (RuleViewModel)group.Children[group.Children.Count - 1];
        condition.SelectedParameter = condition.Parameters.Single(parameter => parameter.Key == key);
        condition.Operator = op;
        condition.Value = value;
        return condition;
    }

    private static async Task<PanelFixture> OpenAllAsync()
    {
        var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Check(fixture.Model.Data.Documents.Select(document => document.Key));
        fixture.Settle();
        fixture.Filter.Library.AddCommand.Execute(null);
        fixture.Settle();
        return fixture;
    }
}
