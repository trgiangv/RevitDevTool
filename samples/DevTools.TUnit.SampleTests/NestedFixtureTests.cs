namespace DevTools.TUnit.SampleTests;

// Scope: nested test classes discovered as separate fixtures.

public sealed class NestedFixtureTests
{
    public sealed class Inner
    {
        [Test]
        public async Task Nested_fixture_is_discovered()
        {
            await Assert.That(XYZ.Zero.IsZeroLength()).IsTrue();
        }
    }
}
