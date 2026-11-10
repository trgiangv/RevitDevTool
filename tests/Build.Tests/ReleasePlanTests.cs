namespace Build.Tests;

[TestClass]
public sealed class ReleasePlanTests
{
    [TestMethod]
    public void Summary_is_one_row_per_field()
    {
        var version = new ProductVersion(Product.PyTest, "0.6.1", false, null);
        var plan = new ReleasePlan(version, "notes", ["a.whl"], [], []);

        var rows = plan.Summary().Split('\n', StringSplitOptions.RemoveEmptyEntries)[1..];

        Assert.DoesNotContain('\r', plan.Summary());
        Assert.AreEqual("| version | 0.6.1 |", rows[2]);
        Assert.IsTrue(rows.All(row => row.StartsWith('|') && row.EndsWith('|')));
    }
}
