using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;

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

        CollectionAssert.AreEquivalent(new[] { "Comments", "Level", "Mark", "Width" },
            filter.Parameters.Select(parameter => parameter.DisplayName).ToArray());
        Assert.AreEqual(ValueKind.Number, rule.Kind);
        Assert.AreEqual(FakeParameters.Width, rule.Parameter);
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

        Assert.AreEqual("Ducts only", fixture.Model.Load().Single().Name);
        Assert.IsFalse(filter.IsDirty);
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
        CollectionAssert.Contains(fixture.Model.HostEvaluations, (FakeModelGenerator.Current, 1L));
    }

    [TestMethod]
    public async Task ReadOnlyHostIgnoresEdits()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.SelectedSource = filter.Sources.First(source => source.Kind == FilterSourceKind.RuleBased);
        fixture.Settle();
        var before = filter.Result;
        var evaluations = fixture.Model.HostEvaluations.Count;

        Category(filter, "Doors").IsChecked = true;
        fixture.Settle();

        Assert.IsFalse(filter.IsDirty);
        Assert.AreSame(before, filter.Result);
        Assert.HasCount(evaluations, fixture.Model.HostEvaluations);
    }

    [TestMethod]
    public async Task SaveFailureShowsMessage()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.AddCommand.Execute(null);
        fixture.Model.StoreError = "Settings file is locked.";

        filter.SaveCommand.Execute(null);

        Assert.AreEqual("Settings file is locked.", fixture.Panel.Message);
        Assert.IsTrue(filter.IsDirty);
        Assert.IsEmpty(fixture.Model.Load());
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

    private static CategoryItemViewModel Category(FilterTabViewModel filter, string name)
    {
        return filter.Categories.First(category => category.Name == name);
    }
}
