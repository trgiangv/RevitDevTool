using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;
using static DevTools.ModelsContext.Tests.TestModel;

namespace DevTools.ModelsContext.Tests.Explorer;

[TestClass]
public sealed class SubcategoryRowTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;

    [TestMethod]
    public void SubgroupKnowsItsParent()
    {
        var snapshot = FakeModelGenerator.BuildSnapshot(WithToposolid().SourceOf(Mep));

        var groups = SnapshotAggregator.GroupsOf(snapshot, snapshot.Counts);

        var sub = groups.Single(group => group.GroupId == SubDivisions);
        Assert.AreEqual(SubDivisionName, sub.Name);
        Assert.AreEqual(Toposolid, sub.ParentId);
        Assert.AreEqual(SubDivisionCount, sub.Count);
        Assert.AreEqual(DevTools.ModelsContext.Documents.GroupRow.TopLevel, groups.Single(group => group.GroupId == Toposolid).ParentId);
    }

    [TestMethod]
    public async Task SubcategoryUncheckDropsRow()
    {
        using var fixture = await PanelFixture.OpenAsync(WithToposolid());
        var before = fixture.Explorer.Rows[0].Count;

        Item(fixture, SubDivisionName).IsChecked = false;

        Assert.IsNull(fixture.FindGroup(Current, SubDivisionName));
        Assert.IsNull(fixture.FindGroup(Current, "Toposolid"), "The parent stayed with no child checked.");
        Assert.AreEqual(before - SubDivisionCount - ToposolidCount, fixture.Explorer.Rows[0].Count);
    }

    [TestMethod]
    public async Task ChildIsolateKeepsOnlyChild()
    {
        using var fixture = await PanelFixture.OpenAsync(WithToposolid());
        fixture.Explorer.Categories.Picker.CheckNoneCommand.Execute(null);
        fixture.Explorer.Categories.Picker.SetChecks([Item(fixture, SubDivisionName)], []);

        await fixture.Explorer.Categories.IsolateCommand.ExecuteAsync(null);

        var step = (CategoryStep)fixture.Actions.ExecutedPlans.Single().Steps.Single();
        Assert.AreEqual(SubDivisions, step.CategoryId);
    }

    [TestMethod]
    public async Task ParentEyeHidesSubcategories()
    {
        using var fixture = await PanelFixture.OpenAsync(WithToposolid());

        await Item(fixture, "Toposolid").ToggleEyeCommand.ExecuteAsync(null);

        var ids = fixture.Actions.ExecutedPlans.Single().Steps.Cast<CategoryStep>().Select(step => step.CategoryId);
        CollectionAssert.AreEquivalent(new[] { Toposolid, SubDivisions }, ids.ToList());
        Assert.IsTrue(Item(fixture, SubDivisionName).IsHiddenInView);
    }

    [TestMethod]
    public async Task ChildEyeHidesOnlyChild()
    {
        using var fixture = await PanelFixture.OpenAsync(WithToposolid());

        await Item(fixture, SubDivisionName).ToggleEyeCommand.ExecuteAsync(null);

        var step = (CategoryStep)fixture.Actions.ExecutedPlans.Single().Steps.Single();
        Assert.AreEqual(SubDivisions, step.CategoryId);
        Assert.IsFalse(Item(fixture, "Toposolid").IsHiddenInView);
    }

    [TestMethod]
    public void ParentRowHidesSubcategoryElements()
    {
        var step = PlanHide(Toposolid);

        Assert.HasCount(ToposolidCount + SubDivisionCount, step.Ids);
    }

    [TestMethod]
    public void ChildRowHidesOnlyItsElements()
    {
        var step = PlanHide(SubDivisions);

        Assert.HasCount(SubDivisionCount, step.Ids);
    }

    private static LinkElementsStep PlanHide(long groupId)
    {
        var data = WithToposolid();
        var snapshot = FakeModelGenerator.BuildSnapshot(data.SourceOf(Mep));
        var row = TestModel.GroupRow(snapshot, Mep, groupId);
        var plan = Planner(data, Mep).Plan(VisibilityRequest.ForRows(VisibilityAction.Hide, row));
        return (LinkElementsStep)plan.Steps.Single();
    }

    private static CategoryItemViewModel Item(PanelFixture fixture, string name)
    {
        return fixture.Explorer.Categories.Picker.Items.First(item => item.Name == name);
    }
}
