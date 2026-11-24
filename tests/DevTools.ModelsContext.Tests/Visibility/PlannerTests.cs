using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Visibility;

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
    public void CategoryRowHidesGroup()
    {
        var row = new TreeRow(TreeRowKind.Category, "Ducts", document: Current, id: TestModel.Ducts);

        var plan = TestModel.Planner(TestModel.SmallData, Current)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Hide, row));

        var step = (CategoryStep)plan.Steps.Single();
        Assert.AreEqual(TestModel.Ducts, step.CategoryId);
    }

    [TestMethod]
    public void LinkModelRowTargetsWholeLink()
    {
        var row = new TreeRow(TreeRowKind.LinkModel, "MEP-Mech.rvt", document: Mep, id: Mep.LinkInstanceId);

        var plan = TestModel.Planner(TestModel.SmallData, Mep)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Halftone, row));

        Assert.AreEqual(new LinkInstanceStep(Mep), plan.Steps.Single());
    }

    [TestMethod]
    public void LinkChildUsesReferenceStep()
    {
        var snapshot = TestModel.Snapshot(Mep);
        var row = TestModel.GroupRow(snapshot, Mep, TestModel.Ducts);

        var plan = TestModel.Planner(TestModel.SmallData, Mep)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Hide, row));

        var step = (LinkElementsStep)plan.Steps.Single();
        Assert.AreEqual(Mep, step.Document);
        Assert.IsTrue(plan.UsesLinkHide);
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
            Assert.AreEqual(ActionState.NoteOf(plan.UsesLinkFilter, plan.UsesLinkHide), check.Note);
        }
    }

    [TestMethod]
    public void LinkIsolateKeepsTargets()
    {
        var snapshot = TestModel.Snapshot(Mep);
        var row = TestModel.GroupRow(snapshot, Mep, TestModel.Ducts);

        var plan = TestModel.Planner(TestModel.SmallData, Mep)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Isolate, row));

        var step = (LinkIsolateStep)plan.Steps.Single();
        Assert.AreEqual(Mep, step.Document);
        Assert.AreEqual(snapshot.IdsUnder(row.Path).Count(), step.KeptIds.Count);
        Assert.IsTrue(plan.UsesLinkHide);
    }

    [TestMethod]
    public void MixedIsolateSplits()
    {
        ElementRef[] elements = [new(Current, 11), new(Mep, 21)];

        var plan = TestModel.Planner(TestModel.SmallData)
            .Plan(VisibilityRequest.ForElements(VisibilityAction.Isolate, elements));

        Assert.IsInstanceOfType<ElementsStep>(plan.Steps[0]);
        CollectionAssert.AreEqual(new long[] { 21 }, ((LinkIsolateStep)plan.Steps[1]).KeptIds.ToArray());
    }

    [TestMethod]
    public void LinkTypeRowColorsByName()
    {
        var snapshot = TestModel.Snapshot(Mep);
        var row = TypeRow(snapshot, Mep, TestModel.Ducts);

        var plan = TestModel.Planner(TestModel.SmallData, Mep)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Color, row) with { Color = new RgbColor(1, 2, 3) });

        var step = (LinkFilterStep)plan.Steps.Single();
        var rules = ((FilterSet)step.Filter.Root.Children.Single()).Children.Cast<FilterRule>().ToList();
        CollectionAssert.AreEqual(new[] { TestModel.Ducts }, step.Filter.Groups.ToArray());
        Assert.AreEqual(LinkRules.FamilyName, rules[0].Parameter);
        Assert.AreEqual(snapshot.FamilyName(row.Path.Type), rules[0].Value);
        Assert.AreEqual(LinkRules.TypeName, rules[1].Parameter);
        Assert.AreEqual(snapshot.TypeName(row.Path.Type), rules[1].Value);
        Assert.AreEqual(snapshot.IdsUnder(row.Path).Count(), step.Ids.Count);
        Assert.IsFalse(plan.UsesLinkHide);
    }

    [TestMethod]
    public void LinkGroupRowColorsCategory()
    {
        var snapshot = TestModel.Snapshot(Mep);
        var row = TestModel.GroupRow(snapshot, Mep, TestModel.Ducts);

        var plan = TestModel.Planner(TestModel.SmallData, Mep)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Halftone, row));

        var step = (LinkFilterStep)plan.Steps.Single();
        Assert.IsEmpty(step.Filter.Root.Children);
        Assert.IsTrue(plan.UsesLinkFilter);
    }

    [TestMethod]
    public void LinkInstanceRowRefusesColor()
    {
        var row = new TreeRow(TreeRowKind.Instance, "Duct", document: Mep, path: RowPath.ForElement(21), id: 21);

        var plan = TestModel.Planner(TestModel.SmallData, Mep)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Halftone, row));

        Assert.AreEqual(VisibilityText.LinkOverrideNeedsRule, plan.DisabledReason);
    }

    [TestMethod]
    public void LinkResultWithoutFilterRefused()
    {
        var plan = TestModel.Planner(TestModel.SmallData, Mep)
            .Plan(VisibilityRequest.ForElements(VisibilityAction.Color, [new ElementRef(Mep, 21)]));

        Assert.AreEqual(VisibilityText.LinkOverrideNeedsRule, plan.DisabledReason);
    }

    [TestMethod]
    public void LinkResultUsesFilterRules()
    {
        var snapshot = TestModel.Snapshot(Mep);
        var id = snapshot.Rows[0].Id;
        var filter = new FilterDefinition([], FilterSet.All(new FilterRule(ParameterKey.BuiltIn("Mark", "ALL_MODEL_MARK"),
            FilterOperator.Equal, "A")));
        ElementRef[] elements = [new(Current, 11), new(Mep, id)];

        var plan = TestModel.Planner(TestModel.SmallData, Current, Mep)
            .Plan(new VisibilityRequest(VisibilityAction.Halftone, [], elements, null, filter));

        var step = plan.Steps.OfType<LinkFilterStep>().Single();
        Assert.AreEqual(filter.Root, step.Filter.Root);
        CollectionAssert.AreEqual(new[] { snapshot.Groups[snapshot.Rows[0].GroupIndex].GroupId }, step.Filter.Groups.ToArray());
        Assert.IsInstanceOfType<ElementsStep>(plan.Steps[0]);
    }

    [TestMethod]
    public void UndefinedRuleIsNotNative()
    {
        var filter = new FilterDefinition([TestModel.Ducts], FilterSet.All(new FilterRule(
            ParameterKey.BuiltIn("Mark", "ALL_MODEL_MARK"), FilterOperator.Undefined)));

        var plan = TestModel.Planner(TestModel.SmallData, Mep)
            .Plan(new VisibilityRequest(VisibilityAction.Color, [], [new ElementRef(Mep, 21)], null, filter));

        Assert.AreEqual(VisibilityText.LinkRuleNotNative, plan.DisabledReason);
    }

    [TestMethod]
    public void LinkRulesOverCapRefused()
    {
        var rules = Enumerable.Range(0, LinkRules.MaxRules + 1)
            .Select(index => (FilterNode)new FilterRule(ParameterKey.BuiltIn("Mark", "ALL_MODEL_MARK"),
                FilterOperator.Equal, index.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
        var filter = new FilterDefinition([TestModel.Ducts], FilterSet.Any(rules));

        var check = TestModel.Planner(TestModel.SmallData, Mep)
            .Check(new VisibilityRequest(VisibilityAction.Color, [], [new ElementRef(Mep, 21)], null, filter));

        Assert.AreEqual(VisibilityText.LinkFilterTooMany, check.DisabledReason);
    }

    [TestMethod]
    public void TypeRowsOverCapRefused()
    {
        var snapshot = TestModel.Snapshot(Mep);
        var rows = Enumerable.Range(0, LinkRules.MaxRules / 2 + 1)
            .Select(index => new TreeRow(TreeRowKind.Type, "T", document: Mep,
                path: RowPath.ForType(0, snapshot.Types[0].FamilyNameIndex, 0)))
            .ToArray();

        var plan = TestModel.Planner(TestModel.SmallData, Mep)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Color, rows));

        Assert.AreEqual(VisibilityText.LinkFilterTooMany, plan.DisabledReason);
    }

    [TestMethod]
    public void LinkCheckFlagsFilter()
    {
        var snapshot = TestModel.Snapshot(Mep);
        var row = TestModel.GroupRow(snapshot, Mep, TestModel.Ducts);

        var check = TestModel.Planner(TestModel.SmallData, Mep)
            .Check(VisibilityRequest.ForRows(VisibilityAction.Color, row));

        Assert.IsNull(check.DisabledReason);
        Assert.AreEqual(VisibilityText.LinkFilterNote, check.Note);
    }

    private static TreeRow TypeRow(DocumentSnapshot snapshot, DocumentKey key, long groupId)
    {
        var group = snapshot.GroupIndexOf(groupId);
        var type = Enumerable.Range(0, snapshot.Types.Count).First(index => snapshot.Types[index].GroupIndex == group);
        return new TreeRow(TreeRowKind.Type, snapshot.TypeName(type), document: key,
            path: RowPath.ForType(group, snapshot.Types[type].FamilyNameIndex, type));
    }

    [TestMethod]
    public void ShowNeedsLinkUnhide()
    {
        var data = FakeModelGenerator.Generate(new FakeModelOptions
        {
            CurrentElements = 100,
            LinkElements = 100,
            LinkCapabilities = DocumentCapabilities.LinkInstance | DocumentCapabilities.LinkElements
        });
        var planner = TestModel.Planner(data);
        ElementRef[] elements = [new(Mep, 1)];

        var show = planner.Plan(VisibilityRequest.ForElements(VisibilityAction.Show, elements));
        var hide = planner.Plan(VisibilityRequest.ForElements(VisibilityAction.Hide, elements));

        Assert.AreEqual(VisibilityText.ShowFromReveal, show.DisabledReason);
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
        CollectionAssert.AreEqual(new long[] { 21 }, ((LinkElementsStep)plan.Steps[1]).Ids.ToArray());
    }

    [TestMethod]
    public void GroupHeaderHasNoActions()
    {
        var header = new TreeRow(TreeRowKind.CurrentModelHeader, "Current model", document: Current);

        var plan = TestModel.Planner(TestModel.SmallData, Current)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Hide, header));

        Assert.AreEqual(VisibilityText.HeaderHasNoActions, plan.DisabledReason);
    }

    [TestMethod]
    public void NoLinkElementsDisablesLinkChild()
    {
        var data = FakeModelGenerator.Generate(new FakeModelOptions
        {
            CurrentElements = 100,
            LinkElements = 100,
            LinkCapabilities = DocumentCapabilities.LinkInstance
        });

        var plan = TestModel.Planner(data)
            .Plan(VisibilityRequest.ForElements(VisibilityAction.Hide, [new ElementRef(Mep, 1)]));

        Assert.AreEqual(VisibilityText.LinkElementsUnavailable, plan.DisabledReason);
    }

    [TestMethod]
    public void MissingSnapshotDisablesRow()
    {
        var snapshot = TestModel.Snapshot(Current);
        var row = TestModel.GroupRow(snapshot, Current, TestModel.Ducts);

        var plan = TestModel.Planner(TestModel.SmallData)
            .Plan(VisibilityRequest.ForRows(VisibilityAction.Hide, row));

        Assert.AreEqual(VisibilityText.Updating, plan.DisabledReason);
    }
}
