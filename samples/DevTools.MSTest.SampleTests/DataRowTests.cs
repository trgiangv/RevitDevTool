namespace DevTools.MSTest.SampleTests;

// MSTest expands these during --list-tests. Each row is its own MTP uid.

[TestClass]
public sealed class DataRowTests
{
    [TestMethod]
    [DataRow(1.0, 0.0, 0.0, DisplayName = "Unit_X")]
    [DataRow(0.0, 1.0, 0.0, DisplayName = "Unit_Y")]
    [DataRow(0.0, 0.0, 1.0, DisplayName = "Unit_Z")]
    public void Named_basis_length_is_one(double x, double y, double z)
    {
        Console.WriteLine($"Testing basis vector ({x}, {y}, {z})");
        Assert.AreEqual(1.0, new XYZ(x, y, z).GetLength(), 1e-9);
    }

    [TestMethod]
    [DynamicData(nameof(MagnitudeCases))]
    public void Magnitude_from_property(double x, double y, double z, double expected)
    {
        Assert.AreEqual(expected, new XYZ(x, y, z).GetLength(), 1e-9);
    }

    public static IEnumerable<object[]> MagnitudeCases
    {
        get
        {
            yield return new object[] { 0.0, 0.0, 0.0, 0.0 };
            yield return new object[] { 0.0, 3.0, 4.0, 5.0 };
            yield return new object[] { -2.0, -3.0, -6.0, 7.0 };
        }
    }

    [TestMethod]
    [DynamicData(nameof(DoubleCaseSource.Rows), typeof(DoubleCaseSource))]
    public void Class_source_without_revit_types(double x, double y, double expected)
    {
        Assert.AreEqual(expected, new XYZ(x, y, 0).GetLength(), 1e-9);
    }
}

public static class DoubleCaseSource
{
    public static IEnumerable<object[]> Rows
    {
        get
        {
            yield return new object[] { 3.0, 4.0, 5.0 };
            yield return new object[] { 0.0, 0.0, 0.0 };
        }
    }
}
