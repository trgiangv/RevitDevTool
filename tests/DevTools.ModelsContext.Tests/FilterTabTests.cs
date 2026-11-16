using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;
using Moq;
using System.Collections.Specialized;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class FilterTabTests
{
    [TestMethod]
    public async Task ParameterListComesFromCatalog()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        filter.Root.AddRuleCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];

        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.DisplayName == "Width");

        CollectionAssert.AreEquivalent(new[] { "Code", "Comments", "Flag", "Level", "Mark", "Swing Angle", "Width" },
            filter.Parameters.Select(parameter => parameter.DisplayName).ToArray());
        Assert.AreEqual(ValueKind.Number, rule.Kind);
        Assert.AreEqual(FakeParameters.Width, rule.Parameter);
    }

    [TestMethod]
    public async Task CatalogMergesAcrossDocuments()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick(fixture.Model.Data.Documents.Select(document => document.Key));
        fixture.Settle();

        var parameters = fixture.Filter.Parameters;
        var width = parameters.Single(parameter => parameter.Key == FakeParameters.Width);

        Assert.AreEqual(ParameterOrigin.Project | ParameterOrigin.Family, width.Origins);
        Assert.AreEqual(ParameterBinding.Instance | ParameterBinding.Type, width.Bindings);
        Assert.AreEqual(2, width.Coverage);
        Assert.AreEqual(3, width.DocumentCount);
        Assert.HasCount(2, parameters.Where(parameter => parameter.DisplayName == "Code").ToList());
    }

    [TestMethod]
    public async Task KindComesFromParameter()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        filter.Root.AddRuleCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];
        rule.Operator = FilterOperator.Contains;

        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.DisplayName == "Level");

        Assert.AreEqual(ValueKind.Reference, rule.Kind);
        Assert.AreEqual(FilterOperator.Equal, rule.Operator);
        Assert.IsFalse(rule.CanBeCaseSensitive);
    }

    [TestMethod]
    public async Task CaseFlagSavesWithRule()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        filter.Root.AddRuleCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];
        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.DisplayName == "Mark");
        rule.Value = "m-001";

        rule.IsCaseSensitive = true;

        var saved = (FilterRule)filter.Definition().Root.Children[0];
        Assert.IsTrue(saved.IsCaseSensitive);
    }

    [TestMethod]
    public async Task ModelEditRecountsWithoutSelecting()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        filter.Root.AddRuleCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];
        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.DisplayName == "Comments");
        rule.Value = "Check";
        fixture.Settle();
        var before = filter.Result.Count;
        var removed = filter.Result.First(element => element.Document == FakeModelGenerator.Current);
        fixture.Model.EchoSelection = false;
        await fixture.Model.SelectAsync([], CancellationToken.None);

        fixture.Model.RaiseElementsChanged(ElementDelta.DeletedOnly(FakeModelGenerator.CurrentSource, [removed.Id]));
        fixture.Settle();

        Assert.AreEqual(before - 1, filter.Result.Count);
        Assert.IsEmpty(await fixture.Model.GetSelectionAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task HostUnitsParseLinkNumbers()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.UnitFactors[FakeModelGenerator.MepSource] = 1000;
        fixture.Panel.ContextBar.Tick([FakeModelGenerator.Current, FakeModelGenerator.Mep]);
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        filter.Root.AddRuleCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];
        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.Key == FakeParameters.Width);
        rule.Value = "300";
        fixture.Settle();

        var snapshot = TestModel.Snapshot(FakeModelGenerator.Mep);
        var expected = snapshot.Rows.Count(row =>
            FakeParameters.TryGet(snapshot, row, FakeParameters.Width, fixture.Model.Data.Levels, out var value)
            && value == "300");
        Assert.IsGreaterThan(0, expected);
        Assert.AreEqual(expected, filter.Result.Count(element => element.Document == FakeModelGenerator.Mep));
    }

    [TestMethod]
    public async Task RuleRemovesItself()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        filter.Root.AddSetCommand.Execute(null);
        var set = (RuleSetViewModel)filter.Root.Children[0];
        set.AddRuleCommand.Execute(null);
        var rule = set.Children[0];

        rule.RemoveCommand.Execute(null);
        set.RemoveCommand.Execute(null);

        Assert.AreEqual(0, set.Children.Count);
        Assert.AreEqual(0, filter.Root.Children.Count);
        Assert.IsTrue(filter.IsDirty);
    }

    [TestMethod]
    public async Task ReadOnlyRuleCannotRemove()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.SelectedSource = filter.Sources.First(source => source.Kind == FilterSourceKind.RuleBased);
        var rule = filter.Root.Children[0];

        Assert.IsFalse(rule.RemoveCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task NewFilterGetsNextName()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        filter.SaveCommand.Execute(null);

        filter.AddCommand.Execute(null);

        Assert.AreEqual("New filter 2", filter.Name);
        Assert.IsTrue(filter.IsEditingName);
    }

    [TestMethod]
    public async Task NameMustBeUnique()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        filter.SaveCommand.Execute(null);
        filter.AddCommand.Execute(null);

        filter.EditName = " new FILTER 1 ";

        Assert.AreEqual(FilterNames.DuplicateName, filter.NameError);
        Assert.IsFalse(filter.SaveCommand.CanExecute(null));
        Assert.IsFalse(filter.CommitRename());
    }

    [TestMethod]
    public async Task EmptyNameBlocksSave()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);

        filter.EditName = "   ";

        Assert.AreEqual(FilterNames.EmptyName, filter.NameError);
        Assert.IsFalse(filter.SaveCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task EscRestoresLastName()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        filter.EditName = string.Empty;

        filter.CancelRename();

        Assert.AreEqual("New filter 1", filter.EditName);
        Assert.IsNull(filter.NameError);
    }

    [TestMethod]
    public async Task SaveStoresTrimmedName()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        filter.EditName = "  Ducts only ";

        filter.SaveCommand.Execute(null);

        Assert.AreEqual("Ducts only", fixture.CustomFilters.Object.Load().Single().Name);
        Assert.IsFalse(filter.IsDirty);
    }

    [TestMethod]
    public async Task EnterRenamesListItem()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        filter.EditName = "Ducts only";

        Assert.IsTrue(filter.CommitRename());

        Assert.AreEqual("Ducts only", filter.SelectedSource!.Name);
    }

    [TestMethod]
    public async Task RenameSavedFilter()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        filter.SaveCommand.Execute(null);

        filter.RenameCommand.Execute(null);
        filter.EditName = "Ducts only";
        filter.CommitRename();
        filter.SaveCommand.Execute(null);

        Assert.AreEqual("Ducts only", fixture.CustomFilters.Object.Load().Single().Name);
    }

    [TestMethod]
    public async Task DiscardRestoresSavedName()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        filter.SaveCommand.Execute(null);
        var saved = filter.SelectedSource!;
        filter.BeginRename();
        filter.EditName = "Renamed";
        filter.CommitRename();

        filter.SelectedSource = filter.Sources.First(source => !source.IsCustom);
        filter.DiscardAndSwitchCommand.Execute(null);

        Assert.AreEqual("New filter 1", saved.Name);
    }

    [TestMethod]
    public async Task SwitchAsksWhenUnsaved()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        var host = filter.Sources.First(source => !source.IsCustom);

        filter.SelectedSource = host;
        var asked = filter.IsSwitchPending;
        filter.DiscardAndSwitchCommand.Execute(null);

        Assert.IsTrue(asked);
        Assert.AreSame(host, filter.SelectedSource);
        Assert.IsFalse(filter.Sources.Any(source => source.IsCustom));
    }

    [TestMethod]
    public async Task HostFilterIsReadOnly()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;

        filter.SelectedSource = filter.Sources.First(source => source.Kind == FilterSourceKind.RuleBased);

        Assert.IsTrue(filter.IsReadOnly);
        Assert.IsFalse(filter.DeleteCommand.CanExecute(null));
        Assert.IsInstanceOfType<UnsupportedRuleViewModel>(filter.Root.Children[1]);
    }

    [TestMethod]
    public async Task HostFilterUsesOwnEvaluation()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        var host = filter.Sources.First(source => source.Kind == FilterSourceKind.RuleBased);

        filter.SelectedSource = host;
        fixture.Settle();

        var expected = fixture.Elements(FakeModelGenerator.Current, 7);
        CollectionAssert.AreEquivalent(expected.ToList(), filter.Result.ToList());
        fixture.HostFilters.Verify(host => host.EvaluateAsync(FakeModelGenerator.Current, 1,
            It.IsAny<IReadOnlyCollection<long>?>(), It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task ReadOnlyHostIgnoresEdits()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.SelectedSource = filter.Sources.First(source => source.Kind == FilterSourceKind.RuleBased);
        fixture.Settle();
        var before = filter.Result;
        fixture.HostFilters.Invocations.Clear();

        Category(filter, "Doors").IsChecked = true;
        fixture.Settle();

        Assert.IsFalse(filter.IsDirty);
        Assert.AreSame(before, filter.Result);
        fixture.HostFilters.Verify(host => host.EvaluateAsync(It.IsAny<DocumentKey>(), It.IsAny<long>(),
            It.IsAny<IReadOnlyCollection<long>?>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [TestMethod]
    public async Task SaveFailureShowsMessage()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        fixture.CustomFilters.Setup(store => store.Save(It.IsAny<IReadOnlyList<CustomFilter>>()))
            .Throws(new InvalidOperationException("Settings file is locked."));

        filter.SaveCommand.Execute(null);

        Assert.AreEqual("Settings file is locked.", fixture.Panel.Message);
        Assert.IsTrue(filter.IsDirty);
        Assert.IsEmpty(fixture.CustomFilters.Object.Load());
    }

    [TestMethod]
    public async Task LiveDebounceFiresOnce()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        var sent = new List<IReadOnlyCollection<ElementRef>>();
        fixture.Model.SelectionChanged += (_, selection) => sent.Add(selection);

        Category(filter, "Doors").IsChecked = true;
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(100));
        Category(filter, "Windows").IsChecked = true;
        fixture.Settle();

        Assert.HasCount(1, sent);
        Assert.AreEqual(filter.ResultCount, sent[0].Count);
    }

    [TestMethod]
    public async Task LivePausesAbove5000()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        fixture.Panel.ContextBar.Tick(fixture.Model.Data.Documents.Select(document => document.Key));
        filter.AddCommand.Execute(null);
        var sent = 0;
        fixture.Model.SelectionChanged += (_, _) => sent++;

        foreach (var category in filter.Categories)
        {
            category.IsChecked = true;
        }

        fixture.Settle();

        Assert.IsGreaterThan(FilterTabViewModel.LivePauseLimit, filter.ResultCount);
        Assert.IsTrue(filter.IsLivePaused);
        Assert.AreEqual(0, sent);
    }

    [TestMethod]
    public async Task AddModeKeepsBaseline()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        var baseline = fixture.Elements(FakeModelGenerator.Current, 1);
        fixture.Model.RaiseSelectionChanged(baseline);
        filter.IsLiveOn = false;
        filter.IsLiveOn = true;
        filter.AddCommand.Execute(null);
        filter.Mode = ResultSelectionMode.Add;

        Category(filter, "Roofs").IsChecked = true;
        fixture.Settle();
        Category(filter, "Roofs").IsChecked = false;
        Category(filter, "Doors").IsChecked = true;
        fixture.Settle();

        var selection = await fixture.Model.GetSelectionAsync(CancellationToken.None);
        CollectionAssert.AreEquivalent(baseline.Union(filter.Result).ToList(), selection.ToList());
    }

    [TestMethod]
    public async Task AllChecksSearchedCategories()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        filter.CategorySearch = "Doors";

        filter.CheckAllCommand.Execute(null);

        CollectionAssert.AreEqual(new[] { "Doors" },
            filter.Categories.Where(category => category.IsChecked).Select(category => category.Name).ToArray());
        Assert.IsTrue(filter.IsDirty);
    }

    [TestMethod]
    public async Task NoneClearsSearchedCategories()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        Category(filter, "Doors").IsChecked = true;
        Category(filter, "Windows").IsChecked = true;
        filter.CategorySearch = "Doors";

        filter.CheckNoneCommand.Execute(null);

        Assert.IsFalse(Category(filter, "Doors").IsChecked);
        Assert.IsTrue(Category(filter, "Windows").IsChecked);
    }

    [TestMethod]
    public async Task ReadOnlyIgnoresCheckAll()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.SelectedSource = filter.Sources.First(source => source.Kind == FilterSourceKind.RuleBased);
        var before = filter.Categories.Where(category => category.IsChecked).ToList();

        filter.CheckAllCommand.Execute(null);

        CollectionAssert.AreEqual(before, filter.Categories.Where(category => category.IsChecked).ToList());
        Assert.IsFalse(filter.IsDirty);
    }

    [TestMethod]
    public async Task UntickLinksKeepsHostFilter()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        var bar = fixture.Panel.ContextBar;
        bar.Tick(fixture.Model.Data.Documents.Select(document => document.Key));
        filter.SelectedSource = filter.Sources.First(source => source.Kind == FilterSourceKind.RuleBased);
        fixture.Settle();
        var expected = filter.Result;
        filter.Sources.CollectionChanged += (_, change) =>
        {
            if (change.Action == NotifyCollectionChangedAction.Reset)
            {
                filter.SelectedSource = null;
            }
        };

        foreach (var item in bar.Documents.Where(item => !item.Key.IsCurrentModel))
        {
            item.IsChecked = false;
        }

        fixture.Settle();

        Assert.IsNull(fixture.Panel.Message);
        Assert.AreEqual("Commented", filter.SelectedSource?.Name);
        CollectionAssert.AreEquivalent(expected.ToList(), filter.Result.ToList());
    }

    [TestMethod]
    public async Task DoorsHidesWidth()
    {
        using var fixture = await OpenAllAsync();
        var filter = fixture.Filter;

        Category(filter, "Doors").IsChecked = true;

        CollectionAssert.Contains(Keys(filter), FakeParameters.SwingAngle);
        CollectionAssert.DoesNotContain(Keys(filter), FakeParameters.Width);
        CollectionAssert.Contains(Keys(filter), FakeParameters.Mark);
    }

    [TestMethod]
    public async Task DuctsHidesSwingAngle()
    {
        using var fixture = await OpenAllAsync();
        var filter = fixture.Filter;

        Category(filter, "Ducts").IsChecked = true;

        CollectionAssert.Contains(Keys(filter), FakeParameters.Width);
        CollectionAssert.DoesNotContain(Keys(filter), FakeParameters.SwingAngle);
    }

    [TestMethod]
    public async Task TwoCategoriesShowUnion()
    {
        using var fixture = await OpenAllAsync();
        var filter = fixture.Filter;

        Category(filter, "Doors").IsChecked = true;
        Category(filter, "Ducts").IsChecked = true;

        CollectionAssert.IsSubsetOf(new[] { FakeParameters.Width, FakeParameters.SwingAngle, FakeParameters.Mark },
            Keys(filter));
    }

    [TestMethod]
    public async Task NoCategoryShowsAll()
    {
        using var fixture = await OpenAllAsync();
        var filter = fixture.Filter;
        Category(filter, "Doors").IsChecked = true;

        filter.CheckNoneCommand.Execute(null);

        CollectionAssert.IsSubsetOf(new[] { FakeParameters.Width, FakeParameters.SwingAngle }, Keys(filter));
    }

    [TestMethod]
    public async Task CoverageCountsCategoryDocuments()
    {
        using var fixture = await OpenAllAsync();
        var filter = fixture.Filter;
        var ducts = Category(filter, "Ducts");
        var expected = fixture.Model.Data.Documents
            .Select(document => TestModel.Snapshot(document.Key))
            .Where(snapshot => snapshot.Groups.Any(group => group.GroupId == ducts.GroupId))
            .Select(snapshot => snapshot.Source)
            .Distinct()
            .Count();

        ducts.IsChecked = true;

        Assert.IsTrue(filter.Parameters.All(parameter => parameter.DocumentCount == expected));
    }

    [TestMethod]
    public async Task RuleKeepsHiddenParameter()
    {
        using var fixture = await OpenAllAsync();
        var filter = fixture.Filter;
        Category(filter, "Doors").IsChecked = true;
        filter.Root.AddRuleCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];
        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.Key == FakeParameters.SwingAngle);
        rule.Operator = FilterOperator.Greater;

        Category(filter, "Doors").IsChecked = false;
        Category(filter, "Ducts").IsChecked = true;

        CollectionAssert.DoesNotContain(Keys(filter), FakeParameters.SwingAngle);
        Assert.AreEqual(FakeParameters.SwingAngle, rule.Parameter);
        Assert.AreEqual("Swing Angle", rule.ParameterName);
        Assert.AreEqual(ValueKind.Number, rule.Kind);
        Assert.AreEqual(FilterOperator.Greater, rule.Operator);
    }

    private static async Task<PanelFixture> OpenAllAsync()
    {
        var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick(fixture.Model.Data.Documents.Select(document => document.Key));
        fixture.Settle();
        fixture.Filter.AddCommand.Execute(null);
        fixture.Settle();
        return fixture;
    }

    private static List<ParameterKey> Keys(FilterTabViewModel filter)
    {
        return filter.Parameters.Select(parameter => parameter.Key).ToList();
    }

    private static CategoryItemViewModel Category(FilterTabViewModel filter, string name)
    {
        return filter.Categories.First(category => category.Name == name);
    }
}
