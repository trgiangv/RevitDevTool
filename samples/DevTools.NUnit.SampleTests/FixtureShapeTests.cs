using Autodesk.Revit.DB;
using NUnit.Framework;

namespace DevTools.NUnit.SampleTests;

// Discover: inherited fixture, nested fixture, [TestFixture(typeof(int))], [TestFixtureSource] of strings.

public abstract class InheritedGeometryTestsBase
{
    [Test]
    public void Identity_transform_basis_is_world()
    {
        var t = Transform.Identity;
        Assert.That(t.BasisX.IsAlmostEqualTo(XYZ.BasisX), Is.True);
        Assert.That(t.BasisY.IsAlmostEqualTo(XYZ.BasisY), Is.True);
        Assert.That(t.BasisZ.IsAlmostEqualTo(XYZ.BasisZ), Is.True);
    }
}

public sealed class InheritedGeometryTests : InheritedGeometryTestsBase;

public sealed class NestedCapabilityTests
{
    [TestFixture]
    public sealed class Inner
    {
        [Test]
        public void Nested_fixture_is_discovered()
        {
            Assert.That(XYZ.Zero.IsZeroLength(), Is.True);
        }
    }
}

[TestFixture(typeof(int))]
public sealed class GenericClosedTests<T>
{
    [Test]
    public void Generic_int_fixture_is_discovered()
    {
        Assert.That(typeof(T), Is.EqualTo(typeof(int)));
    }
}

[TestFixtureSource(nameof(FixtureNames))]
public sealed class NamedFixtureSourceTests
{
    private readonly string _name;

    public NamedFixtureSourceTests(string name) => _name = name;

    public static IEnumerable<string> FixtureNames => ["alpha.rvt", "beta.rvt"];

    [Test]
    public void Fixture_argument_is_preserved()
    {
        Assert.That(_name, Does.EndWith(".rvt"));
    }
}
