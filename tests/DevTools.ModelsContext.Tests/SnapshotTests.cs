using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;
using System.Diagnostics;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class SnapshotTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void CountsMatchGeneratorTotals()
    {
        var data = FakeModelGenerator.Generate();
        var source = data.SourceOf(FakeModelGenerator.Current);
        var stopwatch = Stopwatch.StartNew();

        var snapshot = FakeModelGenerator.BuildSnapshot(source);
        var recount = SnapshotAggregator.Aggregate(snapshot);

        stopwatch.Stop();
        TestContext.WriteLine($"Built and aggregated {snapshot.ElementCount} elements in {stopwatch.ElapsedMilliseconds} ms");
        var groupOfType = source.Types.ToDictionary(type => type.TypeId, type => type.GroupId);
        var expected = source.Elements.GroupBy(element => groupOfType[element.TypeId])
            .ToDictionary(group => group.Key, group => group.Count());
        Assert.AreEqual(100_000, snapshot.Counts.Count);
        Assert.AreEqual(snapshot.Counts.Count, recount.Count);
        foreach (var pair in expected)
        {
            Assert.AreEqual(pair.Value, snapshot.Counts.CountAt(snapshot.GroupIndexOf(pair.Key)));
        }

        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(30));
    }

    [TestMethod]
    public async Task TwoLinkInstancesShareSnapshot()
    {
        var model = new FakeModel(TestModel.SmallData);
        var store = new SnapshotStore(model);
        store.SetDocuments(model.Data.Documents);

        var first = await store.GetAsync(FakeModelGenerator.ArchFirst, null, CancellationToken.None);
        var second = await store.GetAsync(FakeModelGenerator.ArchSecond, null, CancellationToken.None);

        Assert.AreSame(first, second);
        Assert.AreEqual(1, model.BuildCount);
    }

    [TestMethod]
    public async Task DirtySnapshotRebuildsOnUse()
    {
        var model = new FakeModel(TestModel.SmallData);
        var store = new SnapshotStore(model);
        store.SetDocuments(model.Data.Documents);
        var first = await store.GetAsync(FakeModelGenerator.Current, null, CancellationToken.None);
        var ids = first.Rows.Take(DeltaApplier.DirtyThreshold + 1).Select(row => row.Id).ToList();

        store.Apply(ElementDelta.DeletedOnly(FakeModelGenerator.CurrentSource, ids));
        var second = await store.GetAsync(FakeModelGenerator.Current, null, CancellationToken.None);

        Assert.AreNotSame(first, second);
        Assert.IsFalse(second.IsDirty);
    }

    [TestMethod]
    public void SnapshotJsonRoundTripsCounts()
    {
        var snapshot = TestModel.Snapshot(FakeModelGenerator.Mep);

        var copy = SnapshotJson.Deserialize(SnapshotJson.Serialize(snapshot));

        Assert.AreEqual(snapshot.ElementCount, copy.ElementCount);
        Assert.AreEqual(snapshot.Counts.Children.Count, copy.Counts.Children.Count);
        var ducts = snapshot.GroupIndexOf(TestModel.Ducts);
        Assert.AreEqual(snapshot.Counts.CountAt(ducts), copy.Counts.CountAt(copy.GroupIndexOf(TestModel.Ducts)));
    }
}
