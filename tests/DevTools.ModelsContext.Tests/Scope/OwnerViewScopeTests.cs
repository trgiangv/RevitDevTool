using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Scope;

namespace DevTools.ModelsContext.Tests.Scope;

[TestClass]
public sealed class OwnerViewScopeTests
{
    private const long Grid = 1;
    private const long Tag = 2;
    private const long PlanView = 500;
    private static readonly SourceId Source = new("model:owners.rvt");

    [TestMethod]
    public void WholeModelSkipsViewOwned()
    {
        var snapshot = Snapshot();

        Assert.AreEqual(1, snapshot.Counts.Count);
        Assert.AreEqual(1, SnapshotAggregator.Aggregate(snapshot).Count);
        Assert.IsTrue(snapshot.Contains(Tag));
        Assert.IsFalse(snapshot.InModel(Tag));
        CollectionAssert.AreEquivalent(new[] { Grid }, snapshot.ModelIdsUnder(RowPath.ForGroup(0)).ToList());
    }

    [TestMethod]
    public void ActiveViewCountsOwnedElement()
    {
        var snapshot = Snapshot();
        var info = new DocumentInfo(DocumentKey.Current("owners"), Source, "owners", true, DocumentCapabilities.Elements);
        var view = new DocumentScope(info, "owners", snapshot, new HashSet<long> { Grid, Tag });

        Assert.IsTrue(view.InScope(Tag));
        Assert.AreEqual(2, view.Counts.Count);
    }

    private static DocumentSnapshot Snapshot()
    {
        return new DocumentSnapshotBuilder(Source)
            .AddType(new TypeRecord(10, -2000220, "Grids", "Grid", "6.5mm Bubble"))
            .AddElement(new ElementRecord(Grid, 10, 0, 0))
            .AddElement(new ElementRecord(Tag, 10, 0, 0, OwnerViewId: PlanView))
            .Build();
    }
}
