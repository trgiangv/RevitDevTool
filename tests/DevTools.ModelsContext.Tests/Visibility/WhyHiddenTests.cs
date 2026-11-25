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
        var reason = HiddenReasons.Category("Duct Fittings");

        Assert.AreEqual("Category off: Duct Fittings", reason.Text);
        Assert.IsFalse(reason.CanShow);
    }

    [TestMethod]
    public void FilterCuesNameFilter()
    {
        Assert.AreEqual("Filter: Walls-Demo", HiddenReasons.Filter("Walls-Demo").Text);
        Assert.AreEqual("Filter: Walls-Demo (possible)", HiddenReasons.FilterPossible("Walls-Demo").Text);
        Assert.AreEqual(HiddenCause.FilterPossible, HiddenReasons.FilterPossible("Walls-Demo").Cause);
    }

    [TestMethod]
    public void DisplayModeCueNamesMode()
    {
        Assert.AreEqual("By Linked View: link's own settings", HiddenReasons.LinkDisplayMode("By Linked View").Text);
    }

    [TestMethod]
    public void FamilyCueNamesParent()
    {
        var reason = HiddenReasons.FamilySettings("Window-Double-Hung");

        Assert.AreEqual("Hidden by its family: Window-Double-Hung", reason.Text);
        Assert.AreEqual(HiddenCause.NotDrawn, reason.Cause);
        Assert.IsFalse(reason.CanShow);
    }

    [TestMethod]
    public void OnlyShowFixesExist()
    {
        Assert.IsTrue(HiddenReasons.Temporary.CanShow);
        Assert.IsTrue(HiddenReasons.LinkElement.CanShow);
        Assert.IsFalse(HiddenReasons.InView.CanShow);
        Assert.IsFalse(HiddenReasons.LinkTemporary.CanShow);
        Assert.IsFalse(HiddenReasons.WorksetClosed("Shell").CanShow);
        Assert.IsFalse(HiddenReasons.AboveCutPlane.CanShow);
        Assert.IsFalse(HiddenReasons.OutsideCrop.CanShow);
    }

    [TestMethod]
    public void EmptyGivesNoneFound()
    {
        var result = WhyHidden.Of([], 1);

        Assert.AreEqual(HiddenReasons.None, result.First);
        Assert.IsFalse(result.Found);
    }

    [TestMethod]
    public void NoneFoundDropsBesideCauses()
    {
        var result = WhyHidden.Of([HiddenReasons.None, HiddenReasons.OutsideCrop], 1);

        CollectionAssert.AreEqual(new[] { HiddenReasons.OutsideCrop }, result.Reasons.ToArray());
    }

    [TestMethod]
    public void NoneFoundSaysNoSetting()
    {
        Assert.AreEqual("No view setting hides it", HiddenReasons.None.Text);
    }

    [TestMethod]
    public void NotDrawnCuesAreConcrete()
    {
        Assert.AreEqual("No geometry to draw", HiddenReasons.NoGeometry.Text);
        Assert.AreEqual("Not drawn in this view type", HiddenReasons.NotInViewType.Text);
        Assert.AreEqual(HiddenCause.NotDrawn, HiddenReasons.NoGeometry.Cause);
        Assert.IsFalse(HiddenReasons.NotInViewType.CanShow);
    }

    [TestMethod]
    public void UndrawnSaysRevitDoesNotDraw()
    {
        var result = WhyHidden.Of([HiddenReasons.NotDrawnHere], 1);

        Assert.AreEqual("Revit does not draw it in this view", result.First.Text);
        Assert.IsTrue(result.Found);
        Assert.IsFalse(result.First.CanShow);
    }

    [TestMethod]
    public void RailingPartNamesRailing()
    {
        var result = WhyHidden.Of([HiddenReasons.WithRailing], 1);

        Assert.AreEqual("Drawn with its railing", result.First.Text);
        Assert.AreEqual(HiddenCause.Railing, result.First.Cause);
        Assert.IsTrue(result.Found);
        Assert.IsFalse(result.First.CanShow);
    }

    [TestMethod]
    public void SectionDepthCuesNameSide()
    {
        Assert.AreEqual("Behind the section line", HiddenReasons.BehindSectionLine.Text);
        Assert.AreEqual("Behind the elevation line", HiddenReasons.BehindElevationLine.Text);
        Assert.AreEqual("Beyond far clip", HiddenReasons.BeyondFarClip.Text);
        Assert.AreEqual(HiddenCause.OutsideCrop, HiddenReasons.BeyondFarClip.Cause);
    }

    [TestMethod]
    public void CeilingCuesNameRange()
    {
        Assert.AreEqual("Below cut plane", HiddenReasons.BelowCutPlane.Text);
        Assert.AreEqual(HiddenCause.BelowRange, HiddenReasons.BelowCutPlane.Cause);
        Assert.AreEqual("Above view range", HiddenReasons.AboveViewRange.Text);
    }

    [TestMethod]
    public void NotDrawnFollowsPhase()
    {
        var result = WhyHidden.Of([HiddenReasons.AboveCutPlane, HiddenReasons.NoGeometry, HiddenReasons.CreatedLater("Phase 2")], 1);

        Assert.AreEqual(HiddenCause.Phase, result.First.Cause);
        Assert.AreEqual(HiddenCause.NotDrawn, result.Reasons[1].Cause);
    }

    [TestMethod]
    public void NotModelViewStaysAlone()
    {
        var result = WhyHidden.Of([HiddenReasons.NotModelView], 1);

        Assert.AreEqual("Not a model view", result.First.Text);
    }

    [TestMethod]
    public void CausesFollowCheckOrder()
    {
        var result = WhyHidden.Of(
        [
            HiddenReasons.OutsideCrop,
            HiddenReasons.AboveCutPlane,
            HiddenReasons.OwnerView("L1"),
            HiddenReasons.DesignOption("Option 2"),
            HiddenReasons.CreatedLater("Phase 2"),
            HiddenReasons.WorksetClosed("Shell"),
            HiddenReasons.Filter("Walls-Demo"),
            HiddenReasons.Category("Walls"),
            HiddenReasons.InView,
            HiddenReasons.Temporary,
            HiddenReasons.LinkDisplayMode("Custom"),
            HiddenReasons.LinkInstance
        ], 1);

        var causes = result.Reasons.Select(reason => reason.Cause).ToArray();
        CollectionAssert.AreEqual(new[]
        {
            HiddenCause.LinkInstance,
            HiddenCause.LinkDisplayMode,
            HiddenCause.Temporary,
            HiddenCause.InView,
            HiddenCause.Category,
            HiddenCause.Filter,
            HiddenCause.Workset,
            HiddenCause.Phase,
            HiddenCause.DesignOption,
            HiddenCause.OwnerView,
            HiddenCause.AboveRange,
            HiddenCause.OutsideCrop
        }, causes);
    }

    [TestMethod]
    public void SameKindKeepsCheckOrder()
    {
        var result = WhyHidden.Of([HiddenReasons.Filter("B"), HiddenReasons.Filter("A")], 1);

        Assert.AreEqual("Filter: B", result.First.Text);
    }

    [TestMethod]
    public void DuplicateCausesCollapse()
    {
        var result = WhyHidden.Of([HiddenReasons.Category("Walls"), HiddenReasons.Category("Walls")], 1);

        Assert.AreEqual(1, result.Reasons.Count);
    }

    [TestMethod]
    public void JournalHideComesFirst()
    {
        var journal = new JournalPage(1, [new LinkHide(Linked, "uid-7")], false);

        var result = WhyHidden.Of([HiddenReasons.Category("Doors")], 3).WithJournal(journal, Linked, false);

        Assert.AreEqual(HiddenReasons.LinkElement, result.First);
        Assert.AreEqual(2, result.Reasons.Count);
        Assert.AreEqual(3, result.ElapsedMs);
    }

    [TestMethod]
    public void JournalHideReplacesNoneFound()
    {
        var journal = new JournalPage(1, [new LinkHide(Linked, "uid-7")], false);

        var result = WhyHidden.Of([], 1).WithJournal(journal, Linked, false);

        CollectionAssert.AreEqual(new[] { HiddenReasons.LinkElement }, result.Reasons.ToArray());
    }

    [TestMethod]
    public void OtherElementIgnoresJournal()
    {
        var journal = new JournalPage(1, [new LinkHide(new ElementRef(FakeModelGenerator.Mep, 8), "uid-8")], false);
        var before = WhyHidden.Of([], 1);

        Assert.AreSame(before, before.WithJournal(journal, Linked, false));
    }

    [TestMethod]
    public void UserHideLosesShowFix()
    {
        var result = WhyHidden.Of([HiddenReasons.Temporary], 1)
            .WithJournal(JournalPage.Empty, Linked, false);

        Assert.IsFalse(result.First.CanShow);
    }

    [TestMethod]
    public void PanelHideKeepsShowFix()
    {
        var result = WhyHidden.Of([HiddenReasons.Temporary], 1)
            .WithJournal(JournalPage.Empty, Linked, true);

        Assert.IsTrue(result.First.CanShow);
    }
}
