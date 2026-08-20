namespace DevTools.NUnit.Runtime.Tests;

public sealed class NUnitCollapsedSelectionTests
{
    [Fact]
    public void Matches_expanded_fixture_arguments_on_the_declaring_type()
    {
        Assert.True(NUnitCollapsedSelection.Matches(
            "Ns.Fixture.Method",
            "Ns.Fixture(\"a\").Method",
            "Ns.Fixture(\"a\").Method",
            null));
    }

    [Fact]
    public void Matches_does_not_cross_nested_parameterized_suites()
    {
        Assert.False(NUnitCollapsedSelection.Matches(
            "Ns.Fixture.Method",
            "Ns.Fixture(\"a\").Inner(\"b\").Method",
            "Ns.Fixture(\"a\").Inner(\"b\").Method",
            null));
    }
}
