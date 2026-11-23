using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using DevTools.ModelsContext.Tests.Shared;
using Moq;

namespace DevTools.ModelsContext.Tests.Parameters;

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
    public async Task IndexSharedByMatchKey()
    {
        var evaluator = new Mock<IFilterEvaluator>();
        evaluator.Setup(port => port.CountValuesAsync(Current, It.IsAny<ParameterKey>(), It.IsAny<IReadOnlyCollection<long>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParameterValueCounts(3, new Dictionary<string, int> { ["M-001"] = 1, ["m-001"] = 2 }));
        var cache = new ValueIndexCache(evaluator.Object);

        await cache.GetAsync(Current, ParameterKey.BuiltIn("Mark", "ALL_MODEL_MARK"), [], CancellationToken.None);
        var index = await cache.GetAsync(Current, ParameterKey.BuiltIn("Marque", "ALL_MODEL_MARK"), [],
            CancellationToken.None);

        Assert.HasCount(2, index.Values);
        evaluator.Verify(port => port.CountValuesAsync(Current, It.IsAny<ParameterKey>(),
            It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()), Times.Once);
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
