namespace DevTools.MSTest.SampleTests;

// Inherited [TestMethod]s run on the concrete test class.

public abstract class InheritedGeometryTestsBase
{
    [TestMethod]
    public void Identity_transform_basis_is_world()
    {
        var transform = Transform.Identity;
        Assert.IsTrue(transform.BasisX.IsAlmostEqualTo(XYZ.BasisX));
        Assert.IsTrue(transform.BasisY.IsAlmostEqualTo(XYZ.BasisY));
        Assert.IsTrue(transform.BasisZ.IsAlmostEqualTo(XYZ.BasisZ));
    }
}

[TestClass]
public sealed class InheritedGeometryTests : InheritedGeometryTestsBase;
