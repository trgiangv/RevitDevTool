using DevTools.ModelsContext.Parameters;

namespace DevTools.ModelsContext.Tests.Parameters;

[TestClass]
public sealed class RawValueNamesTests
{
    [TestMethod]
    public void PaddedNameMatchesItself()
    {
        var names = new Dictionary<string, long[]> { [" 1/8\" = 1'-0\" "] = [96] };

        Assert.IsTrue(RawValueNames.TryFind(names, " 1/8\" = 1'-0\" ", out var raws));
        CollectionAssert.AreEqual(new long[] { 96 }, raws);
    }

    [TestMethod]
    public void PaddedNamesMergeOnTrim()
    {
        var names = new Dictionary<string, long[]> { ["1:100 "] = [100], [" 1:100"] = [101] };

        Assert.IsTrue(RawValueNames.TryFind(names, "1:100", out var raws));
        CollectionAssert.AreEquivalent(new long[] { 100, 101 }, raws);
    }

    [TestMethod]
    public void UnknownNameFindsNothing()
    {
        var names = new Dictionary<string, long[]> { ["1:50"] = [50] };

        Assert.IsFalse(RawValueNames.TryFind(names, "1:20", out var raws));
        Assert.AreEqual(0, raws.Length);
    }
}
