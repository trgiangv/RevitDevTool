using DevTools.NUnit.Runtime;

namespace DevTools.NUnit.Runtime.Tests;

public sealed class GuardTests
{
    [Fact]
    public void NotNull_returns_value_when_present()
    {
        var value = new object();
        Assert.Same(value, Guard.NotNull(value, nameof(value)));
    }

    [Fact]
    public void NotNull_throws_for_null()
    {
        Assert.Throws<ArgumentNullException>(() => Guard.NotNull<object>(null, "value"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NotNullOrWhiteSpace_rejects_blank_values(string? value)
    {
        var exception = Assert.Throws<ArgumentException>(() => Guard.NotNullOrWhiteSpace(value, "value"));
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void NotNullOrWhiteSpace_returns_non_blank_input()
    {
        Assert.Equal("alpha", Guard.NotNullOrWhiteSpace("alpha", "value"));
    }
}
