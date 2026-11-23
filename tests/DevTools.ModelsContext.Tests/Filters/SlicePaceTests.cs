using DevTools.ModelsContext.Filters;

namespace DevTools.ModelsContext.Tests.Filters;

[TestClass]
public sealed class SlicePaceTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(30);

    [TestMethod]
    public void StartsWithFirstBatch()
    {
        Assert.AreEqual(100, new SlicePace(100, 25, 500).Next(Budget));
    }

    [TestMethod]
    public void FitsBatchToBudget()
    {
        var pace = new SlicePace(100, 25, 500);

        pace.Record(100, TimeSpan.FromMilliseconds(10));

        Assert.AreEqual(300, pace.Next(Budget));
    }

    [TestMethod]
    public void ClampsToBatchLimits()
    {
        var pace = new SlicePace(100, 25, 500);

        pace.Record(100, TimeSpan.FromMilliseconds(1));

        Assert.AreEqual(500, pace.Next(Budget));
        Assert.AreEqual(25, pace.Next(TimeSpan.FromTicks(1)));
    }

    [TestMethod]
    public void SlowBatchShrinksNext()
    {
        var pace = new SlicePace(100, 25, 500);
        pace.Record(100, TimeSpan.FromMilliseconds(10));

        pace.Record(100, TimeSpan.FromMilliseconds(110));

        Assert.IsLessThan(100, pace.Next(Budget));
    }
}
