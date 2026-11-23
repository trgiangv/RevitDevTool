using DevTools.ModelsContext.Parameters;

namespace DevTools.ModelsContext.Tests.Parameters;

[TestClass]
public sealed class ValueTallyTests
{
    [TestMethod]
    public void SumsAcrossDocuments()
    {
        var tally = new ValueTally();

        tally.Add(new ValueIndex([new ValueCount("A", 2), new ValueCount("B", 1)], false, 3));
        tally.Add(new ValueIndex([new ValueCount("A", 1)], true, 4));

        CollectionAssert.AreEqual(new[] { new ValueCount("A", 3), new ValueCount("B", 1) }, tally.Top(10).ToList());
        Assert.IsTrue(tally.HasMore);
        Assert.AreEqual(7, tally.NoParameterCount);
    }

    [TestMethod]
    public void TopOrdersByCountThenName()
    {
        var tally = new ValueTally();
        tally.Add(new ValueIndex([new ValueCount("b", 1), new ValueCount("C", 5), new ValueCount("a", 1)], false, 0));

        var top = tally.Top(2);

        CollectionAssert.AreEqual(new[] { new ValueCount("C", 5), new ValueCount("a", 1) }, top.ToList());
        Assert.IsTrue(tally.IsTruncated(2));
        Assert.IsFalse(tally.IsTruncated(3));
    }

    [TestMethod]
    public void YesNoKeepsBothValues()
    {
        var tally = new ValueTally();
        tally.Add(new ValueIndex([new ValueCount(ValueKinds.Yes, 4)], false, 0));

        CollectionAssert.AreEqual(new[] { new ValueCount(ValueKinds.Yes, 4), new ValueCount(ValueKinds.No, 0) },
            tally.YesNo().ToList());
    }
}
