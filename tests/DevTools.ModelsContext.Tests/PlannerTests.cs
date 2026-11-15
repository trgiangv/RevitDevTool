using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class PlannerTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;

    [TestMethod]
    public void CurrentModelRowHidesById()
    {
        var snapshot = TestModel.Snapshot(Current);
        var row = TestModel.GroupRow(snapshot, Current, TestModel.Ducts);

        var plan = TestModel.Planner(TestModel.SmallData, Current)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Hide, row));

        var step = (ElementsStep)plan.Steps.Single();
        Assert.AreEqual(Current, step.Document);
        Assert.AreEqual(snapshot.Counts.CountAt(row.Path.Group), step.Ids.Count);
    }

    [TestMethod]
    public void AllModelsRowHidesGroup()
    {
        var header = new TreeRow(TreeRowKind.AllModelsHeader, "All models", document: Current);
        var row = new TreeRow(TreeRowKind.AllModelsGroup, "Ducts", header, id: TestModel.Ducts);

        var plan = TestModel.Planner(TestModel.SmallData, Current)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Hide, row));

        var step = (GroupEverywhereStep)plan.Steps.Single();
        Assert.AreEqual(TestModel.Ducts, step.GroupId);
    }

    [TestMethod]
    public void LinkModelRowTargetsWholeLink()
    {
        var row = new TreeRow(TreeRowKind.LinkModel, "MEP-Mech.rvt", document: Mep, id: Mep.LinkInstanceId);

        var plan = TestModel.Planner(TestModel.SmallData, Mep)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Halftone, row));

        Assert.AreEqual(new AttachedModelStep(Mep), plan.Steps.Single());
    }

    [TestMethod]
    public void LinkChildUsesReferenceStep()
    {
        var snapshot = TestModel.Snapshot(Mep);
        var row = TestModel.GroupRow(snapshot, Mep, TestModel.Ducts);

        var plan = TestModel.Planner(TestModel.SmallData, Mep)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Hide, row));

        var step = (ReferenceCommandStep)plan.Steps.Single();
        Assert.AreEqual(Mep, step.Document);
        Assert.IsTrue(plan.UsesReferenceCommands);
    }

    [TestMethod]
    public void CheckMatchesPlanForLinkChild()
    {
        var snapshot = TestModel.Snapshot(Mep);
        var row = TestModel.GroupRow(snapshot, Mep, TestModel.Ducts);
        var planner = TestModel.Planner(TestModel.SmallData, Mep);

        foreach (var action in Enum.GetValues<VisibilityAction>())
        {
            var request = VisibilityRequest.ForRows(action, row);
            var plan = planner.Plan(request);
            var check = planner.Check(request);
            Assert.AreEqual(plan.DisabledReason, check.DisabledReason);
            Assert.AreEqual(plan.UsesReferenceCommands, check.UsesReferenceCommands);
        }
    }

    [TestMethod]
    public void LinkChildOnlyHidesAndShows()
    {
        var snapshot = TestModel.Snapshot(Mep);
        var row = TestModel.GroupRow(snapshot, Mep, TestModel.Ducts);
        var planner = TestModel.Planner(TestModel.SmallData, Mep);

        foreach (var action in new[] { VisibilityAction.Isolate, VisibilityAction.Halftone, VisibilityAction.Color })
        {
            var plan = planner.Plan(VisibilityRequest.ForRows(action, row));
            Assert.AreEqual(VisibilityReasons.LinkChildOnlyHideShow, plan.DisabledReason);
        }

        Assert.IsFalse(planner.Plan(VisibilityRequest.ForRows(VisibilityAction.Show, row)).IsDisabled);
    }

    [TestMethod]
    public void ShowNeedsLinkUnhide()
    {
        var data = FakeModelGenerator.Generate(new FakeModelOptions
        {
            CurrentElements = 100,
            LinkElements = 100,
            LinkCapabilities = DocumentCapabilities.WholeModel | DocumentCapabilities.ReferenceCommands
        });
        var planner = TestModel.Planner(data);
        ElementRef[] elements = [new(Mep, 1)];

        var show = planner.Plan(VisibilityRequest.ForElements(VisibilityAction.Show, elements));
        var hide = planner.Plan(VisibilityRequest.ForElements(VisibilityAction.Hide, elements));

        Assert.AreEqual(VisibilityReasons.ShowFromReveal, show.DisabledReason);
        Assert.IsFalse(hide.IsDisabled);
    }

    [TestMethod]
    public void SelectionAcrossDocumentsSplits()
    {
        ElementRef[] elements = [new(Current, 11), new(Mep, 21), new(Current, 12)];

        var plan = TestModel.Planner(TestModel.SmallData)
            .Plan(VisibilityRequest.ForElements(VisibilityAction.Hide, elements));

        Assert.HasCount(2, plan.Steps);
        CollectionAssert.AreEqual(new long[] { 11, 12 }, ((ElementsStep)plan.Steps[0]).Ids.ToArray());
        CollectionAssert.AreEqual(new long[] { 21 }, ((ReferenceCommandStep)plan.Steps[1]).Ids.ToArray());
    }

    [TestMethod]
    public void GroupHeaderHasNoActions()
    {
        var header = new TreeRow(TreeRowKind.CurrentModelHeader, "Current model", document: Current);

        var plan = TestModel.Planner(TestModel.SmallData, Current)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Hide, header));

        Assert.AreEqual(VisibilityReasons.HeaderHasNoActions, plan.DisabledReason);
    }

    [TestMethod]
    public void NoReferenceCommandsDisablesLinkChild()
    {
        var data = FakeModelGenerator.Generate(new FakeModelOptions
        {
            CurrentElements = 100,
            LinkElements = 100,
            LinkCapabilities = DocumentCapabilities.WholeModel
        });

        var plan = TestModel.Planner(data)
            .Plan(VisibilityRequest.ForElements(VisibilityAction.Hide, [new ElementRef(Mep, 1)]));

        Assert.AreEqual(VisibilityReasons.ReferenceCommandsUnavailable, plan.DisabledReason);
    }

    [TestMethod]
    public void MissingSnapshotDisablesRow()
    {
        var snapshot = TestModel.Snapshot(Current);
        var row = TestModel.GroupRow(snapshot, Current, TestModel.Ducts);

        var plan = TestModel.Planner(TestModel.SmallData)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Hide, row));

        Assert.AreEqual(VisibilityReasons.Updating, plan.DisabledReason);
    }
}
