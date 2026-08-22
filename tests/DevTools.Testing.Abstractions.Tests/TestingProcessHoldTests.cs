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
}
