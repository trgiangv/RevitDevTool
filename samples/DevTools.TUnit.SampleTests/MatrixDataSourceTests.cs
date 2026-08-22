namespace DevTools.TUnit.SampleTests;

// Scope: [MatrixDataSource] combinatorial parameter expansion.

public sealed class MatrixDataSourceTests
{
    [Test]
    [MatrixDataSource]
    public async Task Combinatorial_sign_and_axis(
        [Matrix(-1, 1)] int sign,
        [Matrix("X", "Y")] string axis)
    {
        var vector = axis == "X" ? new XYZ(sign, 0, 0) : new XYZ(0, sign, 0);
        await Assert.That(vector.GetLength()).IsEqualTo(1.0).Within(1e-9);
    }
}
