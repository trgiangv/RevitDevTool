using Autodesk.Revit.DB;

namespace DevTools.TUnit.SampleTests;

// Scope: Revit API types as fixture targets without closed generic base classes.
// Closed generic fixtures (Base<T> with T = XYZ) force RevitAPI native load at
// TUnit testhost registration; per-type fixtures defer that load to in-host run.

public sealed class GenericXyzFixtureTests
{
    [Test]
    public async Task Type_is_defined_in_revit_api()
    {
        await Assert.That(typeof(XYZ).Assembly.GetName().Name).IsEqualTo("RevitAPI");
    }
}

public sealed class GenericBoundingBoxFixtureTests
{
    [Test]
    public async Task Type_is_defined_in_revit_api()
    {
        await Assert.That(typeof(BoundingBoxXYZ).Assembly.GetName().Name).IsEqualTo("RevitAPI");
    }
}

public sealed class GenericElementIdFixtureTests
{
    [Test]
    public async Task Type_is_defined_in_revit_api()
    {
        await Assert.That(typeof(ElementId).Assembly.GetName().Name).IsEqualTo("RevitAPI");
    }
}
