using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.Testing.Abstractions.Tests;

public sealed class TestingProcessHoldTests
{
    [Fact]
    public void GetOrAdd_returns_the_same_instance_for_a_matching_type()
    {
        var first = TestingProcessHold.GetOrAdd("hold-same", static () => new List<int> { 1 });
        var second = TestingProcessHold.GetOrAdd("hold-same", static () => new List<int> { 2 });

        Assert.Same(first, second);
        Assert.Equal([1], first);
    }

    [Fact]
    public void GetOrAdd_does_not_overwrite_when_the_stored_type_does_not_match()
    {
        var stored = TestingProcessHold.GetOrAdd("hold-mismatch", static () => new List<int> { 1 });
        var fallback = TestingProcessHold.GetOrAdd("hold-mismatch", static () => new List<string> { "x" });
        var again = TestingProcessHold.GetOrAdd("hold-mismatch", static () => new List<int> { 9 });

        Assert.Equal(["x"], fallback);
        Assert.Same(stored, again);
        Assert.Equal([1], again);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetOrAdd_rejects_blank_keys(string key)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            TestingProcessHold.GetOrAdd(key, static () => new object()));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void GetOrAdd_rejects_null_factory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TestingProcessHold.GetOrAdd<object>("hold-null-factory-" + Guid.NewGuid().ToString("N"), null!));
    }

    [Fact]
    public void GetOrAdd_rejects_null_factory_result()
    {
        var key = "hold-null-result-" + Guid.NewGuid().ToString("N");
        var exception = Assert.Throws<InvalidOperationException>(() =>
            TestingProcessHold.GetOrAdd<object>(key, static () => null!));

        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }
}
