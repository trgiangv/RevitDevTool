namespace DevTools.TUnit.SampleTests;

// Scope: [InheritsTests] base fixtures sharing tests with derived classes.

public abstract class InheritanceFixtureTestsBase
{
    [Test]
    public async Task Identity_transform_basis_is_world()
    {
        var transform = Transform.Identity;
        await Assert.That(transform.BasisX.IsAlmostEqualTo(XYZ.BasisX)).IsTrue();
        await Assert.That(transform.BasisY.IsAlmostEqualTo(XYZ.BasisY)).IsTrue();
        await Assert.That(transform.BasisZ.IsAlmostEqualTo(XYZ.BasisZ)).IsTrue();
    }
}

[InheritsTests]
public sealed class InheritanceFixtureTests : InheritanceFixtureTestsBase;
