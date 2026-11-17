using DevTools.ModelsContext.Engine;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class ChangeClassifierTests
{
    [TestMethod]
    public void PlainEditDropsValuesOnly()
    {
        var outcome = Classify(Report(added: 3, modified: 5), new TrackedIds([], []));

        Assert.IsFalse(outcome.IsBulk);
        Assert.IsFalse(outcome.DefinitionsChanged);
        Assert.IsFalse(outcome.DocumentsChanged);
        Assert.IsEmpty(outcome.LinkTypes);
    }

    [TestMethod]
    public void BulkChangeDropsAll()
    {
        var called = false;
        var report = Report(added: DeltaApplier.DirtyThreshold, deleted: [1]);

        var outcome = ChangeClassifier.Classify(report, () =>
        {
            called = true;
            return new TrackedIds([], []);
        });

        Assert.IsTrue(outcome.IsBulk);
        Assert.IsTrue(outcome.DefinitionsChanged);
        Assert.IsTrue(outcome.DocumentsChanged);
        Assert.IsFalse(called);
    }

    [TestMethod]
    public void ThresholdItselfIsNotBulk()
    {
        Assert.IsFalse(ChangeClassifier.IsBulk(DeltaApplier.DirtyThreshold, 0, 0));
        Assert.IsTrue(ChangeClassifier.IsBulk(DeltaApplier.DirtyThreshold, 0, 1));
    }

    [TestMethod]
    public void AddedParameterChangesDefinitions()
    {
        var tracked = new TrackedIds([], []);

        var outcome = Classify(Report(added: 1, addedKinds: [new ChangedId(7, ChangedKind.ParameterElement)]), tracked);

        Assert.IsTrue(outcome.DefinitionsChanged);
        Assert.IsFalse(outcome.DocumentsChanged);
        Assert.Contains(7L, tracked.Parameters);
    }

    [TestMethod]
    public void AddedFamilyChangesDefinitions()
    {
        var outcome = Classify(Report(added: 1, addedKinds: [new ChangedId(7, ChangedKind.Family)]), new TrackedIds([], []));

        Assert.IsTrue(outcome.DefinitionsChanged);
    }

    [TestMethod]
    public void ModifiedFamilyChangesDefinitions()
    {
        var outcome = Classify(Report(modified: 1, modifiedKinds: [new ChangedId(7, ChangedKind.Family)]),
            new TrackedIds([], []));

        Assert.IsTrue(outcome.DefinitionsChanged);
    }

    [TestMethod]
    public void AddedLinkChangesDocuments()
    {
        var tracked = new TrackedIds([], []);

        var outcome = Classify(Report(added: 1, addedKinds: [new ChangedId(9, ChangedKind.LinkInstance)]), tracked);

        Assert.IsTrue(outcome.DocumentsChanged);
        Assert.IsFalse(outcome.DefinitionsChanged);
        Assert.Contains(9L, tracked.Links);
    }

    [TestMethod]
    public void AddedLinkTypeChangesDocuments()
    {
        var outcome = Classify(Report(added: 1, addedKinds: [new ChangedId(9, ChangedKind.LinkType)]),
            new TrackedIds([], []));

        Assert.IsTrue(outcome.DocumentsChanged);
    }

    [TestMethod]
    public void ModifiedLinkTypeIsReported()
    {
        var outcome = Classify(Report(modified: 1, modifiedKinds: [new ChangedId(9, ChangedKind.LinkType)]),
            new TrackedIds([], []));

        CollectionAssert.AreEqual(new long[] { 9 }, outcome.LinkTypes.ToArray());
        Assert.IsFalse(outcome.DocumentsChanged);
    }

    [TestMethod]
    public void ModifiedLinkInstanceIsIgnored()
    {
        var outcome = Classify(Report(modified: 1, modifiedKinds: [new ChangedId(9, ChangedKind.LinkInstance)]),
            new TrackedIds([], []));

        Assert.IsFalse(outcome.DocumentsChanged);
        Assert.IsEmpty(outcome.LinkTypes);
    }

    [TestMethod]
    public void DeletedLinkChangesDocuments()
    {
        var tracked = new TrackedIds([9], []);

        var outcome = Classify(Report(deleted: [9]), tracked);

        Assert.IsTrue(outcome.DocumentsChanged);
        Assert.IsEmpty(tracked.Links);
    }

    [TestMethod]
    public void DeletedParameterChangesDefinitions()
    {
        var tracked = new TrackedIds([], [7]);

        var outcome = Classify(Report(deleted: [7]), tracked);

        Assert.IsTrue(outcome.DefinitionsChanged);
        Assert.IsEmpty(tracked.Parameters);
    }

    [TestMethod]
    public void DeletedUntrackedIsPlain()
    {
        var outcome = Classify(Report(deleted: [5]), new TrackedIds([9], [7]));

        Assert.IsFalse(outcome.DocumentsChanged);
        Assert.IsFalse(outcome.DefinitionsChanged);
    }

    private static ChangeOutcome Classify(ChangeReport report, TrackedIds tracked)
    {
        return ChangeClassifier.Classify(report, () => tracked);
    }

    private static ChangeReport Report(
        int added = 0,
        int modified = 0,
        ChangedId[]? addedKinds = null,
        ChangedId[]? modifiedKinds = null,
        long[]? deleted = null)
    {
        return new ChangeReport(added, modified, addedKinds ?? [], modifiedKinds ?? [], deleted ?? []);
    }
}
