using Autodesk.Revit.DB;
using NUnit.Framework;

namespace DevTools.NUnit.SampleTests;

// Discover: [Theory], Combinatorial, Sequential, Pairwise, [Range].

[TestFixture]
public sealed class ValueSourceTests
{
    [Theory]
    public void Theory_values_are_combinatorial(
        [Values(0.0, 1.0, -2.0)] double x,
        [Values(0.0, 3.0)] double y)
    {
        Assert.That(new XYZ(x, y, 0).GetLength(), Is.GreaterThanOrEqualTo(0.0));
    }

    [Test]
    [Combinatorial]
    public void Combinatorial_sign_and_axis(
        [Values(-1, 1)] int sign,
        [Values("X", "Y")] string axis)
    {
        var v = axis == "X" ? new XYZ(sign, 0, 0) : new XYZ(0, sign, 0);
        Assert.That(v.GetLength(), Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    [Sequential]
    public void Sequential_pairs_length(
        [Values(3.0, 0.0)] double x,
        [Values(4.0, 1.0)] double y)
    {
        Assert.That(new XYZ(x, y, 0).GetLength(), Is.GreaterThanOrEqualTo(1.0));
    }

    [Test]
    [Pairwise]
    public void Pairwise_three_factors(
        [Values(1, 2, 3)] int a,
        [Values("n", "s")] string b,
        [Values(true, false)] bool c)
    {
        Assert.That(a, Is.GreaterThan(0));
        Assert.That(b, Is.Not.Empty);
        Assert.That(c || !c, Is.True);
    }

    [Test]
    public void Range_is_expanded([Range(1, 3)] int n)
    {
        Assert.That(n, Is.InRange(1, 3));
    }
}
