namespace DevTools.TUnit.SampleTests;

// Scope: [MethodDataSource] with scalar tuples and external static case classes.

public sealed class MethodDataSourceScalarTests
{
    [Test]
    [MethodDataSource(nameof(MagnitudeCases))]
    public async Task Magnitude_from_tuple_source(double x, double y, double z, double expected)
    {
        await Assert.That(new XYZ(x, y, z).GetLength()).IsEqualTo(expected).Within(1e-9);
    }

    public static IEnumerable<(double X, double Y, double Z, double Expected)> MagnitudeCases()
    {
        yield return (0.0, 0.0, 0.0, 0.0);
        yield return (0.0, 3.0, 4.0, 5.0);
        yield return (-2.0, -3.0, -6.0, 7.0);
    }

    [Test]
    [MethodDataSource(typeof(ScalarCaseSource), nameof(ScalarCaseSource.Cases))]
    public async Task Magnitude_from_class_source(double x, double y, double expected)
    {
        await Assert.That(new XYZ(x, y, 0).GetLength()).IsEqualTo(expected).Within(1e-9);
    }
}

public static class ScalarCaseSource
{
    public static IEnumerable<(double X, double Y, double Expected)> Cases()
    {
        yield return (3.0, 4.0, 5.0);
        yield return (0.0, 0.0, 0.0);
    }
}
