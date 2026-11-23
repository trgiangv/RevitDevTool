using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Visibility;

[TestClass]
public sealed class WhyHiddenTests
{
    private static readonly ElementRef Linked = new(FakeModelGenerator.Mep, 7);

    [TestMethod]
    public void CategoryCueNamesCategory()
    {
        var cause = HiddenCues.Category("Duct Fittings");

        Assert.AreEqual("Category off: Duct Fittings", cause.Text);
        Assert.AreEqual(HiddenFix.None, cause.Fix);
    }

    [TestMethod]
    public void FilterCuesNameFilter()
    {
        Assert.AreEqual("Filter: Walls-Demo", HiddenCues.Filter("Walls-Demo").Text);
        Assert.AreEqual("Filter: Walls-Demo (possible)", HiddenCues.FilterPossible("Walls-Demo").Text);
        Assert.AreEqual(HiddenCauseKind.FilterPossible, HiddenCues.FilterPossible("Walls-Demo").Kind);
    }

    [TestMethod]
    public void DisplayModeCueNamesMode()
    {
        Assert.AreEqual("By Linked View: link's own settings", HiddenCues.LinkDisplayMode("By Linked View").Text);
    }

    [TestMethod]
    public void FamilyCueNamesParent()
    {
        var cause = HiddenCues.FamilySettings("Window-Double-Hung");

        Assert.AreEqual("Hidden by its family: Window-Double-Hung", cause.Text);
        Assert.AreEqual(HiddenCauseKind.NotDrawn, cause.Kind);
        Assert.AreEqual(HiddenFix.None, cause.Fix);
    }

    [TestMethod]
    public void OnlyShowFixesExist()
    {
        Assert.AreEqual(HiddenFix.ShowElement, HiddenCues.TemporaryHideIsolate.Fix);
        Assert.AreEqual(HiddenFix.ShowElement, HiddenCues.HiddenInLink.Fix);
        Assert.AreEqual(HiddenFix.None, HiddenCues.HiddenInView.Fix);
        Assert.AreEqual(HiddenFix.None, HiddenCues.LinkTemporary.Fix);
        Assert.AreEqual(HiddenFix.None, HiddenCues.WorksetClosed("Shell").Fix);
        Assert.AreEqual(HiddenFix.None, HiddenCues.AboveCutPlane.Fix);
        Assert.AreEqual(HiddenFix.None, HiddenCues.OutsideCrop.Fix);
    }

    [TestMethod]
    public void EmptyGivesNoneFound()
    {
        var result = WhyHiddenResult.Of([], 1);

        Assert.AreEqual(HiddenCues.NoneFound, result.First);
        Assert.IsFalse(result.Found);
    }

    [TestMethod]
    public void NoneFoundDropsBesideCauses()
    {
        var result = WhyHiddenResult.Of([HiddenCues.NoneFound, HiddenCues.OutsideCrop], 1);

        CollectionAssert.AreEqual(new[] { HiddenCues.OutsideCrop }, result.Causes.ToArray());
    }

    [TestMethod]
    public void NoneFoundSaysNoSetting()
    {
        Assert.AreEqual("No view setting hides it", HiddenCues.NoneFound.Text);
    }

    [TestMethod]
    public void NotDrawnCuesAreConcrete()
    {
        Assert.AreEqual("No geometry to draw", HiddenCues.NoGeometry.Text);
        Assert.AreEqual("Not drawn in this view type", HiddenCues.NotInViewType.Text);
        Assert.AreEqual(HiddenCauseKind.NotDrawn, HiddenCues.NoGeometry.Kind);
        Assert.AreEqual(HiddenFix.None, HiddenCues.NotInViewType.Fix);
    }

    [TestMethod]
    public void CeilingCuesNameRange()
    {
        Assert.AreEqual("Below cut plane", HiddenCues.BelowCutPlane.Text);
        Assert.AreEqual(HiddenCauseKind.BelowViewDepth, HiddenCues.BelowCutPlane.Kind);
        Assert.AreEqual("Above view range", HiddenCues.AboveViewRange.Text);
    }

    [TestMethod]
    public void NotDrawnFollowsPhase()
    {
        var result = WhyHiddenResult.Of([HiddenCues.AboveCutPlane, HiddenCues.NoGeometry, HiddenCues.CreatedLater("Phase 2")], 1);

        Assert.AreEqual(HiddenCauseKind.Phase, result.First.Kind);
        Assert.AreEqual(HiddenCauseKind.NotDrawn, result.Causes[1].Kind);
    }

    [TestMethod]
    public void NotModelViewStaysAlone()
    {
        var result = WhyHiddenResult.Of([HiddenCues.NotModelView], 1);

        Assert.AreEqual("Not a model view", result.First.Text);
    }

    [TestMethod]
    public void CausesFollowCheckOrder()
    {
        var result = WhyHiddenResult.Of(
        [
            HiddenCues.OutsideCrop,
            HiddenCues.AboveCutPlane,
            HiddenCues.OwnerView("L1"),
            HiddenCues.DesignOption("Option 2"),
            HiddenCues.CreatedLater("Phase 2"),
            HiddenCues.WorksetClosed("Shell"),
            HiddenCues.Filter("Walls-Demo"),
            HiddenCues.Category("Walls"),
            HiddenCues.HiddenInView,
            HiddenCues.TemporaryHideIsolate,
            HiddenCues.LinkDisplayMode("Custom"),
            HiddenCues.LinkHidden
        ], 1);

        var kinds = result.Causes.Select(cause => cause.Kind).ToArray();
        CollectionAssert.AreEqual(new[]
        {
            HiddenCauseKind.LinkHidden,
            HiddenCauseKind.LinkDisplayMode,
            HiddenCauseKind.TemporaryHideIsolate,
            HiddenCauseKind.HiddenInView,
            HiddenCauseKind.Category,
            HiddenCauseKind.Filter,
            HiddenCauseKind.Workset,
            HiddenCauseKind.Phase,
            HiddenCauseKind.DesignOption,
            HiddenCauseKind.OwnerView,
            HiddenCauseKind.AboveCutPlane,
            HiddenCauseKind.OutsideCrop
        }, kinds);
    }

    [TestMethod]
    public void SameKindKeepsCheckOrder()
    {
        var result = WhyHiddenResult.Of([HiddenCues.Filter("B"), HiddenCues.Filter("A")], 1);

        Assert.AreEqual("Filter: B", result.First.Text);
    }

    [TestMethod]
    public void DuplicateCausesCollapse()
    {
        var result = WhyHiddenResult.Of([HiddenCues.Category("Walls"), HiddenCues.Category("Walls")], 1);

        Assert.AreEqual(1, result.Causes.Count);
    }

    [TestMethod]
    public void JournalHideComesFirst()
    {
        var journal = new JournalState(1, [new JournalEntry(Linked, "uid-7")], false);

        var result = WhyHiddenResult.Of([HiddenCues.Category("Doors")], 3).WithJournal(journal, Linked);

        Assert.AreEqual(HiddenCues.HiddenInLink, result.First);
        Assert.AreEqual(2, result.Causes.Count);
        Assert.AreEqual(3, result.ElapsedMs);
    }

    [TestMethod]
    public void JournalHideReplacesNoneFound()
    {
        var journal = new JournalState(1, [new JournalEntry(Linked, "uid-7")], false);

        var result = WhyHiddenResult.Of([], 1).WithJournal(journal, Linked);

        CollectionAssert.AreEqual(new[] { HiddenCues.HiddenInLink }, result.Causes.ToArray());
    }

    [TestMethod]
    public void OtherElementIgnoresJournal()
    {
        var journal = new JournalState(1, [new JournalEntry(new ElementRef(FakeModelGenerator.Mep, 8), "uid-8")], false);
        var before = WhyHiddenResult.Of([], 1);

        Assert.AreSame(before, before.WithJournal(journal, Linked));
    }
}
