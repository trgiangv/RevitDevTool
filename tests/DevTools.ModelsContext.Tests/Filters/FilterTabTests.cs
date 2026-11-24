using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using Moq;
using System.Collections.Specialized;
using System.Globalization;

namespace DevTools.ModelsContext.Tests.Filters;

[TestClass]
public sealed class FilterTabTests
{
    [TestMethod]
    public async Task ParameterListComesFromCatalog()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Root.AddConditionCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];

        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.DisplayName == "Width");

        CollectionAssert.AreEquivalent(new[]
            {
                "Code", "Comments", "Fire Rating", "Flag", "Function", "Insulation", "Level", "Mark", "Swing Angle",
                "System Type", "Width"
            },
            filter.Root.Parameters.Select(parameter => parameter.DisplayName).ToArray());
        Assert.AreEqual(ValueKind.Number, rule.Kind);
        Assert.AreEqual(FakeParameters.Width, rule.Parameter);
    }

    [TestMethod]
    public async Task CatalogMergesAcrossDocuments()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick(fixture.Model.Data.Documents.Select(document => document.Key));
        fixture.Settle();

        var parameters = fixture.Filter.Root.Parameters;
        var width = parameters.Single(parameter => parameter.Key == FakeParameters.Width);

        Assert.AreEqual(ParameterOrigin.Project | ParameterOrigin.Family, width.Origins);
        Assert.AreEqual(ParameterBinding.Instance | ParameterBinding.Type, width.Bindings);
        Assert.HasCount(2, width.Models);
        Assert.HasCount(2, parameters.Where(parameter => parameter.DisplayName == "Code").ToList());
    }

    [TestMethod]
    public async Task KindComesFromParameter()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Root.AddConditionCommand.Execute(null);
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
        filter.Library.AddCommand.Execute(null);
        filter.Root.AddConditionCommand.Execute(null);
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
        filter.Library.AddCommand.Execute(null);
        filter.Root.AddConditionCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];
        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.DisplayName == "Comments");
        rule.Value = "Check";
        fixture.Settle();
        var before = filter.Runner.Result.Count;
        var removed = filter.Runner.Result.First(element => element.Document == FakeModelGenerator.Current);
        fixture.Model.EchoSelection = false;
        await fixture.Model.SelectAsync([], CancellationToken.None);

        fixture.Model.RaiseElementsChanged(ElementDelta.DeletedOnly(FakeModelGenerator.CurrentSource, [removed.Id]));
        fixture.Settle();

        Assert.AreEqual(before - 1, filter.Runner.Result.Count);
        Assert.IsEmpty(await fixture.Model.GetSelectionAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task NewFilterSummaryReadsNone()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Filter.Library.AddCommand.Execute(null);

        Assert.AreEqual(CategoryPickerViewModel.NoCategories, fixture.Filter.Categories.Summary);
    }

    [TestMethod]
    public async Task ConditionSummaryReadsAll()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        var raised = new List<string?>();
        filter.Categories.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        filter.Root.AddConditionCommand.Execute(null);

        Assert.AreEqual(CategoryPickerViewModel.AllCategories, filter.Categories.Summary);
        CollectionAssert.Contains(raised, nameof(CategoryPickerViewModel.Summary));
    }

    [TestMethod]
    public async Task UnloadedLinkSkipsFilter()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([FakeModelGenerator.Current, FakeModelGenerator.Mep]);
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Root.AddConditionCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];
        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.DisplayName == "Comments");
        rule.Value = "Check";
        fixture.Settle();

        fixture.Model.SetLoaded(FakeModelGenerator.Mep, false);
        fixture.Settle();
        rule.Value = "Check ";
        rule.Value = "Check";
        fixture.Settle();

        Assert.IsNull(fixture.Panel.Message);
        Assert.IsTrue(filter.Runner.Result.Any(element => element.Document == FakeModelGenerator.Current));
        Assert.IsFalse(filter.Runner.Result.Any(element => element.Document == FakeModelGenerator.Mep));
    }

    [TestMethod]
    public async Task TypedNumberUsesCurrentUnits()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.UnitFactors[FakeModelGenerator.MepSource] = 1000;
        fixture.Panel.ContextBar.Tick([FakeModelGenerator.Current, FakeModelGenerator.Mep]);
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Root.AddConditionCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];
        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.Key == FakeParameters.Width);
        rule.Value = "300";
        fixture.Settle();

        var snapshot = TestModel.Snapshot(FakeModelGenerator.Mep);
        var expected = snapshot.Rows.Count(row =>
            FakeParameters.TryGet(snapshot, row, FakeParameters.Width, fixture.Model.Data.Levels, out var value)
            && value == "300");
        Assert.IsGreaterThan(0, expected);
        Assert.AreEqual(expected, filter.Runner.Result.Count(element => element.Document == FakeModelGenerator.Mep));
    }

    [TestMethod]
    public async Task PickedValueKeepsStoredNumber()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.DisplayFactors[FakeModelGenerator.MepSource] = 1000;
        fixture.Panel.ContextBar.Tick([FakeModelGenerator.Current, FakeModelGenerator.Mep]);
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Root.AddConditionCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];
        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.Key == FakeParameters.Width);
        await rule.LoadValuesCommand.ExecuteAsync(null);

        rule.PickedValue = rule.Values.Single(value => value.Value == "0.3");
        fixture.Settle();

        var snapshot = TestModel.Snapshot(FakeModelGenerator.Mep);
        var expected = snapshot.Rows.Count(row =>
            FakeParameters.TryGet(snapshot, row, FakeParameters.Width, fixture.Model.Data.Levels, out var value)
            && value == "300");
        Assert.IsGreaterThan(0, expected);
        Assert.AreEqual("0.3", rule.Value);
        Assert.AreEqual(expected, filter.Runner.Result.Count(element => element.Document == FakeModelGenerator.Mep));
    }

    [TestMethod]
    public async Task TypingDropsPickedNumber()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.DisplayFactors[FakeModelGenerator.MepSource] = 1000;
        fixture.Panel.ContextBar.Tick([FakeModelGenerator.Current, FakeModelGenerator.Mep]);
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Root.AddConditionCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];
        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.Key == FakeParameters.Width);
        await rule.LoadValuesCommand.ExecuteAsync(null);
        rule.PickedValue = rule.Values.Single(value => value.Value == "0.3");

        rule.Value = "0.35";
        fixture.Settle();

        Assert.IsNull(rule.Picked);
        Assert.AreEqual(0, filter.Runner.Result.Count(element => element.Document == FakeModelGenerator.Mep));
    }

    [TestMethod]
    public async Task PlaceholderUsesCurrentModel()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.Examples[FakeParameters.LengthSpec] = "1' - 0\"";
        fixture.Panel.ContextBar.Tick([FakeModelGenerator.Current, FakeModelGenerator.Mep]);
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Root.AddConditionCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];

        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.Key == FakeParameters.Width);
        fixture.Settle();

        Assert.AreEqual("1' - 0\"", rule.ValueHint);
        Assert.IsTrue(fixture.Model.ExampleRequests.All(model => model == FakeModelGenerator.Current));
    }

    [TestMethod]
    public async Task TextHasNoPlaceholder()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Root.AddConditionCommand.Execute(null);
        var rule = (RuleViewModel)filter.Root.Children[0];
        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.Key == FakeParameters.Width);
        fixture.Settle();

        rule.SelectedParameter = rule.Parameters.Single(parameter => parameter.Key == FakeParameters.Mark);
        fixture.Settle();

        Assert.IsNull(rule.ValueHint);
    }

    [TestMethod]
    public async Task RuleRemovesItself()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Root.AddGroupCommand.Execute(null);
        var set = (RuleSetViewModel)filter.Root.Children[0];
        set.AddConditionCommand.Execute(null);
        var rule = set.Children[0];

        rule.RemoveCommand.Execute(null);
        set.RemoveCommand.Execute(null);

        Assert.AreEqual(0, set.Children.Count);
        Assert.AreEqual(0, filter.Root.Children.Count);
        Assert.IsTrue(filter.Library.IsDirty);
    }

    [TestMethod]
    public async Task ReadOnlyRuleCannotRemove()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.SelectedSource = filter.Library.Sources.First(source => source.Kind == FilterSourceKind.RuleBased);
        var rule = filter.Root.Children[0];

        Assert.IsFalse(rule.RemoveCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task NewFilterGetsNextName()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Library.SaveCommand.Execute(null);

        filter.Library.AddCommand.Execute(null);

        Assert.AreEqual("New filter 2", filter.Library.Name);
        Assert.IsTrue(filter.Library.IsEditingName);
    }

    [TestMethod]
    public async Task NameMustBeUnique()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Library.SaveCommand.Execute(null);
        filter.Library.AddCommand.Execute(null);

        filter.Library.EditName = " new FILTER 1 ";

        Assert.AreEqual(FilterNames.DuplicateName, filter.Library.NameError);
        Assert.IsFalse(filter.Library.SaveCommand.CanExecute(null));
        Assert.IsFalse(filter.Library.CommitRename());
    }

    [TestMethod]
    public async Task EmptyNameBlocksSave()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);

        filter.Library.EditName = "   ";

        Assert.AreEqual(FilterNames.EmptyName, filter.Library.NameError);
        Assert.IsFalse(filter.Library.SaveCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task EscRestoresLastName()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Library.EditName = string.Empty;

        filter.Library.CancelRename();

        Assert.AreEqual("New filter 1", filter.Library.EditName);
        Assert.IsNull(filter.Library.NameError);
    }

    [TestMethod]
    public async Task SaveStoresTrimmedName()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Library.EditName = "  Ducts only ";

        filter.Library.SaveCommand.Execute(null);

        Assert.AreEqual("Ducts only", fixture.CustomFilters.Object.Load().Single().Name);
        Assert.IsFalse(filter.Library.IsDirty);
    }

    [TestMethod]
    public async Task EnterRenamesListItem()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Library.EditName = "Ducts only";

        Assert.IsTrue(filter.Library.CommitRename());

        Assert.AreEqual("Ducts only", filter.Library.SelectedSource!.Name);
    }

    [TestMethod]
    public async Task RenameSavedFilter()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Library.SaveCommand.Execute(null);

        filter.Library.RenameCommand.Execute(null);
        filter.Library.EditName = "Ducts only";
        filter.Library.CommitRename();
        filter.Library.SaveCommand.Execute(null);

        Assert.AreEqual("Ducts only", fixture.CustomFilters.Object.Load().Single().Name);
    }

    [TestMethod]
    public async Task DiscardRestoresSavedName()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Library.SaveCommand.Execute(null);
        var saved = filter.Library.SelectedSource!;
        filter.Library.BeginRename();
        filter.Library.EditName = "Renamed";
        filter.Library.CommitRename();

        filter.Library.SelectedSource = filter.Library.Sources.First(source => !source.IsCustom);
        filter.Library.DiscardAndSwitchCommand.Execute(null);

        Assert.AreEqual("New filter 1", saved.Name);
    }

    [TestMethod]
    public async Task SwitchAsksWhenUnsaved()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        var host = filter.Library.Sources.First(source => !source.IsCustom);

        filter.Library.SelectedSource = host;
        var asked = filter.Library.IsSwitchPending;
        filter.Library.DiscardAndSwitchCommand.Execute(null);

        Assert.IsTrue(asked);
        Assert.AreSame(host, filter.Library.SelectedSource);
        Assert.IsFalse(filter.Library.Sources.Any(source => source.IsCustom));
    }

    [TestMethod]
    public async Task HostFilterIsReadOnly()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;

        filter.Library.SelectedSource = filter.Library.Sources.First(source => source.Kind == FilterSourceKind.RuleBased);

        Assert.IsTrue(filter.IsReadOnly);
        Assert.IsFalse(filter.Library.DeleteCommand.CanExecute(null));
        Assert.IsInstanceOfType<UnsupportedRuleViewModel>(filter.Root.Children[1]);
    }

    [TestMethod]
    public async Task HostFilterUsesOwnEvaluation()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        var host = filter.Library.Sources.First(source => source.Kind == FilterSourceKind.RuleBased);

        filter.Library.SelectedSource = host;
        fixture.Settle();

        var expected = fixture.Elements(FakeModelGenerator.Current, 7);
        CollectionAssert.AreEquivalent(expected.ToList(), filter.Runner.Result.ToList());
        fixture.HostFilters.Verify(host => host.EvaluateAsync(FakeModelGenerator.Current, 1,
            It.IsAny<IReadOnlyCollection<long>?>(), It.IsAny<CancellationToken>()));
    }

    [TestMethod]
    public async Task ReadOnlyHostIgnoresEdits()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.SelectedSource = filter.Library.Sources.First(source => source.Kind == FilterSourceKind.RuleBased);
        fixture.Settle();
        var before = filter.Runner.Result;
        fixture.HostFilters.Invocations.Clear();

        Category(filter, "Doors").IsChecked = true;
        fixture.Settle();

        Assert.IsFalse(filter.Library.IsDirty);
        Assert.AreSame(before, filter.Runner.Result);
        fixture.HostFilters.Verify(host => host.EvaluateAsync(It.IsAny<DocumentKey>(), It.IsAny<long>(),
            It.IsAny<IReadOnlyCollection<long>?>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [TestMethod]
    public async Task SaveFailureShowsMessage()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        fixture.CustomFilters.Setup(store => store.Save(It.IsAny<IReadOnlyList<CustomFilter>>()))
            .Throws(new InvalidOperationException("Settings file is locked."));

        filter.Library.SaveCommand.Execute(null);

        Assert.AreEqual("Settings file is locked.", fixture.Panel.Message);
        Assert.IsTrue(filter.Library.IsDirty);
        Assert.IsEmpty(fixture.CustomFilters.Object.Load());
    }

    [TestMethod]
    public async Task LiveDebounceFiresOnce()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        var sent = new List<IReadOnlyCollection<ElementRef>>();
        fixture.Model.SelectionChanged += (_, selection) => sent.Add(selection);

        Category(filter, "Doors").IsChecked = true;
        fixture.Clock.Advance(TimeSpan.FromMilliseconds(100));
        Category(filter, "Windows").IsChecked = true;
        fixture.Settle();

        Assert.HasCount(1, sent);
        Assert.AreEqual(filter.Runner.ResultCount, sent[0].Count);
    }

    [TestMethod]
    public async Task LivePausesAbove5000()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        fixture.Panel.ContextBar.Tick(fixture.Model.Data.Documents.Select(document => document.Key));
        filter.Library.AddCommand.Execute(null);
        var sent = 0;
        fixture.Model.SelectionChanged += (_, _) => sent++;

        foreach (var category in filter.Categories.Items)
        {
            category.IsChecked = true;
        }

        fixture.Settle();

        Assert.IsGreaterThan(FilterRunner.LivePauseLimit, filter.Runner.ResultCount);
        Assert.IsTrue(filter.Runner.IsLivePaused);
        Assert.IsTrue(filter.Runner.IsLiveOn);
        StringAssert.Contains(FilterRunner.LivePausedText, FilterRunner.LivePauseLimit.ToString("N0", CultureInfo.CurrentCulture));
        Assert.AreEqual(0, sent);
    }

    [TestMethod]
    public async Task PauseEndsUnderLimit()
    {
        using var fixture = await PausedAsync();
        var filter = fixture.Filter;
        var sent = 0;
        fixture.Model.SelectionChanged += (_, _) => sent++;

        filter.Categories.CheckNoneCommand.Execute(null);
        Category(filter, "Doors").IsChecked = true;
        fixture.Settle();

        Assert.IsFalse(filter.Runner.IsLivePaused);
        Assert.AreEqual(1, sent);
    }

    [TestMethod]
    public async Task PauseEndsForEmptyFilter()
    {
        using var fixture = await PausedAsync();

        fixture.Filter.Categories.CheckNoneCommand.Execute(null);
        fixture.Settle();

        Assert.AreEqual(0, fixture.Filter.Runner.ResultCount);
        Assert.IsFalse(fixture.Filter.Runner.IsLivePaused);
    }

    [TestMethod]
    public async Task PauseEndsWhenLiveOff()
    {
        using var fixture = await PausedAsync();

        fixture.Filter.Runner.IsLiveOn = false;

        Assert.IsFalse(fixture.Filter.Runner.IsLivePaused);
    }

    [TestMethod]
    public async Task PauseEndsWithNoFilter()
    {
        using var fixture = await PausedAsync();

        fixture.Filter.Library.DeleteCommand.Execute(null);
        fixture.Settle();

        Assert.IsNull(fixture.Filter.Library.SelectedSource);
        Assert.IsFalse(fixture.Filter.Runner.IsLivePaused);
    }

    private static async Task<PanelFixture> PausedAsync()
    {
        var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick(fixture.Model.Data.Documents.Select(document => document.Key));
        fixture.Filter.Library.AddCommand.Execute(null);
        foreach (var category in fixture.Filter.Categories.Items)
        {
            category.IsChecked = true;
        }

        fixture.Settle();
        Assert.IsTrue(fixture.Filter.Runner.IsLivePaused);
        return fixture;
    }

    [TestMethod]
    public async Task AddModeKeepsBaseline()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        var baseline = fixture.Elements(FakeModelGenerator.Current, 1);
        fixture.Model.RaiseSelectionChanged(baseline);
        filter.Runner.IsLiveOn = false;
        filter.Runner.IsLiveOn = true;
        filter.Library.AddCommand.Execute(null);
        filter.Runner.Mode = ResultSelectionMode.Add;

        Category(filter, "Roofs").IsChecked = true;
        fixture.Settle();
        Category(filter, "Roofs").IsChecked = false;
        Category(filter, "Doors").IsChecked = true;
        fixture.Settle();

        var selection = await fixture.Model.GetSelectionAsync(CancellationToken.None);
        CollectionAssert.AreEquivalent(baseline.Union(filter.Runner.Result).ToList(), selection.ToList());
    }

    [TestMethod]
    public async Task AllChecksSearchedCategories()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        filter.Categories.Search = "Doors";

        filter.Categories.CheckAllCommand.Execute(null);

        CollectionAssert.AreEqual(new[] { "Doors" },
            filter.Categories.Items.Where(category => category.IsChecked).Select(category => category.Name).ToArray());
        Assert.IsTrue(filter.Library.IsDirty);
    }

    [TestMethod]
    public async Task NoneClearsSearchedCategories()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        Category(filter, "Doors").IsChecked = true;
        Category(filter, "Windows").IsChecked = true;
        filter.Categories.Search = "Doors";

        filter.Categories.CheckNoneCommand.Execute(null);

        Assert.IsFalse(Category(filter, "Doors").IsChecked);
        Assert.IsTrue(Category(filter, "Windows").IsChecked);
    }

    [TestMethod]
    public async Task ReadOnlyIgnoresCheckAll()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.SelectedSource = filter.Library.Sources.First(source => source.Kind == FilterSourceKind.RuleBased);
        var before = filter.Categories.Items.Where(category => category.IsChecked).ToList();

        filter.Categories.CheckAllCommand.Execute(null);

        CollectionAssert.AreEqual(before, filter.Categories.Items.Where(category => category.IsChecked).ToList());
        Assert.IsFalse(filter.Library.IsDirty);
    }

    [TestMethod]
    public async Task UntickLinksKeepsHostFilter()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        var bar = fixture.Panel.ContextBar;
        bar.Tick(fixture.Model.Data.Documents.Select(document => document.Key));
        filter.Library.SelectedSource = filter.Library.Sources.First(source => source.Kind == FilterSourceKind.RuleBased);
        fixture.Settle();
        var expected = filter.Runner.Result;
        filter.Library.Sources.CollectionChanged += (_, change) =>
        {
            if (change.Action == NotifyCollectionChangedAction.Reset)
            {
                filter.Library.SelectedSource = null;
            }
        };

        foreach (var item in bar.Documents.Where(item => !item.Key.IsCurrentModel))
        {
            item.IsChecked = false;
        }

        fixture.Settle();

        Assert.IsNull(fixture.Panel.Message);
        Assert.AreEqual("Commented", filter.Library.SelectedSource?.Name);
        CollectionAssert.AreEquivalent(expected.ToList(), filter.Runner.Result.ToList());
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

        filter.Categories.CheckNoneCommand.Execute(null);

        CollectionAssert.IsSubsetOf(new[] { FakeParameters.Width, FakeParameters.SwingAngle }, Keys(filter));
    }

    [TestMethod]
    public async Task ModelsListCategoryDocuments()
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

        var mark = filter.Root.Parameters.Single(parameter => parameter.Key == FakeParameters.Mark);
        Assert.HasCount(expected, mark.Models);
    }

    [TestMethod]
    public async Task RuleKeepsHiddenParameter()
    {
        using var fixture = await OpenAllAsync();
        var filter = fixture.Filter;
        Category(filter, "Doors").IsChecked = true;
        filter.Root.AddConditionCommand.Execute(null);
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
        fixture.Filter.Library.AddCommand.Execute(null);
        fixture.Settle();
        return fixture;
    }

    private static List<ParameterKey> Keys(FilterTabViewModel filter)
    {
        return filter.Root.Parameters.Select(parameter => parameter.Key).ToList();
    }

    private static CategoryItemViewModel Category(FilterTabViewModel filter, string name)
    {
        return filter.Categories.Items.First(category => category.Name == name);
    }
}
