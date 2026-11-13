using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class AggregationTests
{
    [TestMethod]
    public void AllModelsMergesCategories()
    {
        var current = TestModel.Snapshot(FakeModelGenerator.Current);
        var mep = TestModel.Snapshot(FakeModelGenerator.Mep);
        var documents = new Dictionary<DocumentKey, DocumentSnapshot>
        {
            [FakeModelGenerator.Current] = current,
            [FakeModelGenerator.Mep] = mep
        };

        var merged = SnapshotAggregator.MergeGroups(documents);

        var ducts = merged.Single(group => group.GroupId == TestModel.Ducts);
        var expected = current.Counts.CountAt(current.GroupIndexOf(TestModel.Ducts))
                       + mep.Counts.CountAt(mep.GroupIndexOf(TestModel.Ducts));
        Assert.AreEqual(expected, ducts.Count);
        Assert.HasCount(2, ducts.Documents);
        Assert.AreEqual(merged.Count, merged.Select(group => group.GroupId).Distinct().Count());
    }

    [TestMethod]
    public void LinkInstancesCountEach()
    {
        var arch = TestModel.Snapshot(FakeModelGenerator.ArchFirst);
        var documents = new Dictionary<DocumentKey, DocumentSnapshot>
        {
            [FakeModelGenerator.ArchFirst] = arch,
            [FakeModelGenerator.ArchSecond] = arch
        };

        var merged = SnapshotAggregator.MergeGroups(documents);

        Assert.AreEqual(2 * arch.ElementCount, merged.Sum(group => group.Count));
    }

    [TestMethod]
    public async Task ScopeCountsViewIds()
    {
        var model = new FakeModel(TestModel.SmallData);
        var snapshot = TestModel.Snapshot(FakeModelGenerator.Current);
        var ids = await model.CollectScopeAsync(FakeModelGenerator.Current, ScopeKind.ActiveView, CancellationToken.None);

        var counts = ScopeCounter.Count(snapshot, ids.Append(-1));

        Assert.AreEqual(ids.Count, counts.Count);
        Assert.IsLessThan(snapshot.ElementCount, counts.Count);
    }
}
