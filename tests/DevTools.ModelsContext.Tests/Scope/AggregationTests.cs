using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests.Scope;

[TestClass]
public sealed class AggregationTests
{
    [TestMethod]
    public void MergeSumsCategories()
    {
        var current = TestModel.Snapshot(FakeModelGenerator.Current);
        var mep = TestModel.Snapshot(FakeModelGenerator.Mep);

        var merged = SnapshotAggregator.MergeGroups([GroupsOf(current), GroupsOf(mep)]);

        var ducts = merged.Single(group => group.GroupId == TestModel.Ducts);
        var expected = current.Counts.CountAt(current.GroupIndexOf(TestModel.Ducts))
                       + mep.Counts.CountAt(mep.GroupIndexOf(TestModel.Ducts));
        Assert.AreEqual(expected, ducts.Count);
        Assert.AreEqual(merged.Count, merged.Select(group => group.GroupId).Distinct().Count());
    }

    [TestMethod]
    public void LinkInstancesCountEach()
    {
        var arch = TestModel.Snapshot(FakeModelGenerator.ArchFirst);

        var merged = SnapshotAggregator.MergeGroups([GroupsOf(arch), GroupsOf(arch)]);

        Assert.AreEqual(2 * arch.ElementCount, merged.Sum(group => group.Count));
    }

    [TestMethod]
    public void MergedGroupsSortByName()
    {
        var current = TestModel.Snapshot(FakeModelGenerator.Current);
        var mep = TestModel.Snapshot(FakeModelGenerator.Mep);

        var merged = SnapshotAggregator.MergeGroups([GroupsOf(current), GroupsOf(mep)]);

        CollectionAssert.AreEqual(
            merged.Select(group => group.Name).OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            merged.Select(group => group.Name).ToList());
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

    private static IReadOnlyList<GroupCount> GroupsOf(DocumentSnapshot snapshot)
    {
        return SnapshotAggregator.GroupsOf(snapshot, snapshot.Counts);
    }
}
