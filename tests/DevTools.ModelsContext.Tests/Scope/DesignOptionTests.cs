using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Scope;

namespace DevTools.ModelsContext.Tests.Scope;

[TestClass]
public sealed class DesignOptionTests
{
    private const long Main = 1;
    private const long Primary = 2;
    private const long Secondary = 3;
    private static readonly SourceId Source = new("model:options.rvt");

    [TestMethod]
    public void WholeModelSkipsSecondaryOption()
    {
        var snapshot = Snapshot();
        var group = RowPath.ForGroup(0);

        Assert.AreEqual(2, snapshot.Counts.Count);
        Assert.AreEqual(2, SnapshotAggregator.Aggregate(snapshot).Count);
        Assert.IsTrue(snapshot.Contains(Secondary));
        Assert.IsFalse(snapshot.InModel(Secondary));
        CollectionAssert.AreEquivalent(new[] { Main, Primary }, snapshot.ModelIdsUnder(group).ToList());
        CollectionAssert.AreEquivalent(new[] { Main, Primary, Secondary }, snapshot.IdsUnder(group).ToList());
    }

    [TestMethod]
    public void ViewScopeCountsShownOption()
    {
        var snapshot = Snapshot();

        var counts = ScopeCounter.Count(snapshot, [Main, Secondary]);

        Assert.AreEqual(2, counts.Count);
    }

    [TestMethod]
    public void ScopesFollowOptionRule()
    {
        var snapshot = Snapshot();
        var info = new DocumentInfo(DocumentKey.Current("options"), Source, "options", true, DocumentCapabilities.WholeModel);
        var whole = new DocumentScope(info, "options", snapshot, null);
        var view = new DocumentScope(info, "options", snapshot, new HashSet<long> { Main, Secondary });

        Assert.IsFalse(whole.InScope(Secondary));
        CollectionAssert.AreEquivalent(new[] { Main, Primary }, whole.IdsUnder(RowPath.ForGroup(0)).ToList());
        Assert.IsTrue(view.InScope(Secondary));
        CollectionAssert.AreEquivalent(new[] { Main, Secondary }, view.IdsUnder(RowPath.ForGroup(0)).ToList());
        Assert.AreEqual(2, view.Counts.Count);
    }

    [TestMethod]
    public void OptionSwitchMovesCount()
    {
        var snapshot = Snapshot();

        DeltaApplier.Apply(snapshot, new ElementDelta(Source, [], [],
            [new ElementRecord(Primary, 10, 0, 0, true), new ElementRecord(Secondary, 10, 0, 0)], []));

        Assert.AreEqual(2, snapshot.Counts.Count);
        Assert.IsFalse(snapshot.InModel(Primary));
        Assert.IsTrue(snapshot.InModel(Secondary));
    }

    [TestMethod]
    public void AddedSecondaryNotCounted()
    {
        var snapshot = Snapshot();

        DeltaApplier.Apply(snapshot, new ElementDelta(Source, [], [new ElementRecord(4, 10, 0, 0, true)], [], []));

        Assert.AreEqual(2, snapshot.Counts.Count);
        Assert.IsTrue(snapshot.Contains(4));
        Assert.IsFalse(snapshot.InModel(4));
    }

    private static DocumentSnapshot Snapshot()
    {
        return new DocumentSnapshotBuilder(Source)
            .AddType(new TypeRecord(10, -2000011, "Walls", "Basic Wall", "Exterior"))
            .AddElement(new ElementRecord(Main, 10, 0, 0))
            .AddElement(new ElementRecord(Primary, 10, 0, 0))
            .AddElement(new ElementRecord(Secondary, 10, 0, 0, true))
            .Build();
    }
}
