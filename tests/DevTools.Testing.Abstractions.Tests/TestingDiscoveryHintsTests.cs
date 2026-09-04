using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Tests;

public sealed class TestingDiscoveryHintsTests
{
    [Fact]
    public void Empty_is_empty_for_all_hint_lists()
    {
        Assert.True(TestingDiscoveryHints.Empty.IsEmpty);
        Assert.True(new TestingDiscoveryHints().IsEmpty);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void IsEmpty_is_false_when_any_hint_list_has_values(
        bool classNames,
        bool methodNames,
        bool categories)
    {
        var hints = new TestingDiscoveryHints(
            ClassNames: classNames ? ["Smoke"] : null,
            MethodNames: methodNames ? ["Run"] : null,
            Categories: categories ? ["Host"] : null);

        Assert.False(hints.IsEmpty);
    }

    [Fact]
    public void Empty_hint_lists_are_treated_as_blank()
    {
        Assert.True(new TestingDiscoveryHints(ClassNames: [], MethodNames: [], Categories: []).IsEmpty);
    }
}
