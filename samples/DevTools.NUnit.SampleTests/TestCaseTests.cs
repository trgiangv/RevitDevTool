using System.Collections;
using Autodesk.Revit.DB;
using NUnit.Framework;

namespace DevTools.NUnit.SampleTests;

// Discover: [TestCase] / TestName, [TestCaseSource] of primitives.

[TestFixture]
public sealed class TestCaseTests
{
    [TestCase(1.0, 0.0, 0.0, TestName = "Unit_X")]
    [TestCase(0.0, 1.0, 0.0, TestName = "Unit_Y")]
    [TestCase(0.0, 0.0, 1.0, TestName = "Unit_Z")]
    public void Named_basis_length_is_one(double x, double y, double z)
    {
        Assert.That(new XYZ(x, y, z).GetLength(), Is.EqualTo(1.0).Within(1e-9));
    }

    [TestCaseSource(nameof(MagnitudeCases))]
    public void Magnitude_from_object_source(double x, double y, double z, double expected)
    {
        Assert.That(new XYZ(x, y, z).GetLength(), Is.EqualTo(expected).Within(1e-9));
    }

    public static IEnumerable<TestCaseData> MagnitudeCases()
    {
        yield return new TestCaseData(0.0, 0.0, 0.0, 0.0).SetName("Zero");
        yield return new TestCaseData(0.0, 3.0, 4.0, 5.0).SetName("3-4-5");
        yield return new TestCaseData(-2.0, -3.0, -6.0, 7.0).SetName("Length7");
    }

    [TestCaseSource(typeof(DoubleCaseSource))]
    public void Class_source_without_revit_types(double x, double y, double expected)
    {
        Assert.That(new XYZ(x, y, 0).GetLength(), Is.EqualTo(expected).Within(1e-9));
    }
}

public sealed class DoubleCaseSource : IEnumerable<TestCaseData>
{
    public IEnumerator<TestCaseData> GetEnumerator()
    {
        yield return new TestCaseData(3.0, 4.0, 5.0).SetName("ClassSource_3-4-5");
        yield return new TestCaseData(0.0, 0.0, 0.0).SetName("ClassSource_Zero");
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
