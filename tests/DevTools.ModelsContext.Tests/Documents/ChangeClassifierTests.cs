using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Tests.Documents;

[TestClass]
public sealed class ChangeClassifierTests
{
    [TestMethod]
    public void PlainEditKeepsDefinitions()
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

    [TestMethod]
    public void ValueEditDropsValuesOnly()
    {
        var outcome = Classify(Report(modified: 4), new TrackedIds([9], [7]));

        Assert.AreEqual(CacheDrop.Values, outcome.Drop);
    }

    [TestMethod]
    public void TypeEditDropsResolved()
    {
        var outcome = Classify(Report(modified: 2, modifiedKinds: [new ChangedId(3, ChangedKind.ElementType)]),
            new TrackedIds([], []));

        Assert.AreEqual(CacheDrop.Resolved, outcome.Drop);
    }

    [TestMethod]
    public void AddedElementDropsCandidates()
    {
        var outcome = Classify(Report(added: 1, modified: 2), new TrackedIds([], []));

        Assert.AreEqual(CacheDrop.Candidates, outcome.Drop);
    }

    [TestMethod]
    public void DeletedElementDropsCandidates()
    {
        var outcome = Classify(Report(modified: 1, deleted: [5]), new TrackedIds([], []));

        Assert.AreEqual(CacheDrop.Candidates, outcome.Drop);
    }

    [TestMethod]
    public void OptionEditDropsCandidates()
    {
        var outcome = Classify(Report(modified: 1, modifiedKinds: [new ChangedId(4, ChangedKind.DesignOption)]),
            new TrackedIds([], []));

        Assert.AreEqual(CacheDrop.Candidates, outcome.Drop);
    }

    [TestMethod]
    public void DefinitionEditDropsAll()
    {
        var outcome = Classify(Report(modified: 2, modifiedKinds:
            [new ChangedId(3, ChangedKind.ElementType), new ChangedId(7, ChangedKind.ParameterElement)]),
            new TrackedIds([], []));

        Assert.AreEqual(CacheDrop.All, outcome.Drop);
    }

    [TestMethod]
    public void BulkEditDropsAll()
    {
        var outcome = Classify(Report(modified: DeltaApplier.DirtyThreshold + 1), new TrackedIds([], []));

        Assert.AreEqual(CacheDrop.All, outcome.Drop);
    }

    [TestMethod]
    public void LinkReloadDropsValuesOnly()
    {
        var outcome = Classify(Report(modified: 1, modifiedKinds: [new ChangedId(9, ChangedKind.LinkType)]),
            new TrackedIds([], []));

        Assert.AreEqual(CacheDrop.Values, outcome.Drop);
    }

    [TestMethod]
    public void UnknownEditDropsAll()
    {
        var outcome = Classify(Report(modified: 1, modifiedKinds: [new ChangedId(6, ChangedKind.Unknown)]),
            new TrackedIds([], []));

        Assert.AreEqual(CacheDrop.All, outcome.Drop);
        Assert.IsTrue(outcome.DefinitionsChanged);
    }

    [TestMethod]
    public void DeletedFamilyChangesDefinitions()
    {
        var tracked = new TrackedIds([], [], [8]);

        var outcome = Classify(Report(deleted: [8]), tracked);

        Assert.IsTrue(outcome.DefinitionsChanged);
        Assert.AreEqual(CacheDrop.All, outcome.Drop);
        Assert.IsEmpty(tracked.Families);
    }

    [TestMethod]
    public void AddedFamilyIsTracked()
    {
        var tracked = new TrackedIds([], []);

        Classify(Report(added: 1, addedKinds: [new ChangedId(8, ChangedKind.Family)]), tracked);

        Assert.Contains(8L, tracked.Families);
    }

    [TestMethod]
    public void ResolvedKeptWhenEditOutsideCandidates()
    {
        Assert.IsTrue(ChangeClassifier.KeepsResolved(CacheDrop.Values, 1, [5], id => id == 9));
    }

    [TestMethod]
    public void ResolvedDroppedWhenCandidateEdited()
    {
        Assert.IsFalse(ChangeClassifier.KeepsResolved(CacheDrop.Values, 1, [5, 9], id => id == 9));
    }

    [TestMethod]
    public void ResolvedDroppedWithoutCandidates()
    {
        Assert.IsFalse(ChangeClassifier.KeepsResolved(CacheDrop.Values, 1, [5], null));
    }

    [TestMethod]
    public void UnresolvedKeyDropped()
    {
        Assert.IsFalse(ChangeClassifier.KeepsResolved(CacheDrop.Values, 0, [], _ => false));
    }

    [TestMethod]
    public void TypeEditDropsResolvedEntries()
    {
        Assert.IsFalse(ChangeClassifier.KeepsResolved(CacheDrop.Resolved, 1, [], _ => false));
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
