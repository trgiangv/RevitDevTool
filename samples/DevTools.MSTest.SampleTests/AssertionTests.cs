namespace DevTools.MSTest.SampleTests;

// ThrowsExactly, collection, and string asserts against values produced in the host.

[TestClass]
public sealed class AssertionTests
{
    [TestMethod]
    public void Throws_exactly_the_expected_exception()
    {
        var message = XYZ.BasisX.GetLength().ToString("0");
        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            throw new InvalidOperationException(message));
        Assert.AreEqual(message, error.Message);
    }

    [TestMethod]
    public async Task Throws_exactly_async()
    {
        var message = XYZ.BasisY.GetLength().ToString("0");
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException(message);
        });
        Assert.AreEqual(message, error.Message);
    }

    [TestMethod]
    public void Collection_and_string_asserts()
    {
        var axes = new[] { XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ };
        Assert.HasCount(3, axes);
        Assert.IsTrue(axes[1].IsAlmostEqualTo(XYZ.BasisY));
        var labels = string.Join(",", axes.Select(axis => axis.X.ToString("0")));
        Assert.StartsWith("1", labels);
        Assert.Contains(",0,", labels);
    }
}
