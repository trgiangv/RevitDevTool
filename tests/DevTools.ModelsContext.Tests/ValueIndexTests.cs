using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class ValueIndexTests
{
    private static readonly SourceId Current = FakeModelGenerator.CurrentSource;

    [TestMethod]
    public async Task ValueIndexCapsAt500()
    {
        var cache = new ValueIndexCache(new FakeModel(TestModel.SmallData));

        var index = await cache.GetAsync(Current, FakeParameters.Mark, [], CancellationToken.None);

        Assert.HasCount(ValueIndexCache.MaxValues, index.Values);
        Assert.IsTrue(index.HasMore);
    }

    [TestMethod]
    public async Task NoParameterIsTotalMinusHits()
    {
        var snapshot = TestModel.Snapshot(FakeModelGenerator.Current);
        var cache = new ValueIndexCache(new FakeModel(TestModel.SmallData));

        var index = await cache.GetAsync(Current, FakeParameters.Width, [TestModel.Ducts, TestModel.Walls],
            CancellationToken.None);

        Assert.AreEqual(snapshot.Counts.CountAt(snapshot.GroupIndexOf(TestModel.Walls)), index.NoParameterCount);
    }

    [TestMethod]
    public async Task CacheKeepsEightIndexes()
    {
        var cache = new ValueIndexCache(new FakeModel(TestModel.SmallData));
        long[] groups = [TestModel.Ducts, TestModel.Walls, TestModel.Doors];

        foreach (var group in groups)
        {
            foreach (var parameter in new[] { FakeParameters.Mark, FakeParameters.Comments, FakeParameters.Level })
            {
                await cache.GetAsync(Current, parameter, [group], CancellationToken.None);
            }
        }

        Assert.AreEqual(ValueIndexCache.DefaultCapacity, cache.Count);
        Assert.IsFalse(cache.Contains(Current, FakeParameters.Mark, [TestModel.Ducts]));
    }

    [TestMethod]
    public async Task InvalidateDropsSourceIndexes()
    {
        var cache = new ValueIndexCache(new FakeModel(TestModel.SmallData));
        await cache.GetAsync(Current, FakeParameters.Mark, [], CancellationToken.None);
        await cache.GetAsync(FakeModelGenerator.MepSource, FakeParameters.Mark, [], CancellationToken.None);

        cache.Invalidate(Current);

        Assert.AreEqual(1, cache.Count);
    }
}
