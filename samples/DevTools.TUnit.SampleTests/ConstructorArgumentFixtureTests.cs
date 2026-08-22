namespace DevTools.TUnit.SampleTests;

// Scope: primary-constructor [Arguments] rows for string fixture parameters.

[Arguments("alpha.rvt")]
[Arguments("beta.rvt")]
public sealed class ConstructorArgumentFixtureTests(string modelName)
{
    [Test]
    public async Task Fixture_argument_is_preserved()
    {
        await Assert.That(modelName).EndsWith(".rvt");
    }
}
