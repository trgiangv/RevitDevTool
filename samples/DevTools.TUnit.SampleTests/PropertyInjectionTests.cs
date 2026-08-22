namespace DevTools.TUnit.SampleTests;

// Scope: [Arguments] on writable properties injected before each test.

public sealed class PropertyInjectionTests
{
    [Arguments(3.0)]
    public double Leg { get; set; }

    [Test]
    public async Task Injected_leg_is_positive()
    {
        await Assert.That(Leg).IsGreaterThan(0.0);
    }
}
