namespace DevTools.TUnit.SampleTests;

// Scope: inline [Arguments] rows with optional DisplayName.

public sealed class ArgumentsDataSourceTests
{
    [Test]
    [Arguments(1.0, 0.0, 0.0, DisplayName = "Unit_X")]
    [Arguments(0.0, 1.0, 0.0, DisplayName = "Unit_Y")]
    [Arguments(0.0, 0.0, 1.0, DisplayName = "Unit_Z")]
    public async Task Named_basis_length_is_one(double x, double y, double z)
    {
        await Assert.That(new XYZ(x, y, z).GetLength()).IsEqualTo(1.0).Within(1e-9);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task Range_is_expanded(int n)
    {
        await Assert.That(n).IsGreaterThanOrEqualTo(1);
        await Assert.That(n).IsLessThanOrEqualTo(3);
    }
}
