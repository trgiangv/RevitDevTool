using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.Testing.Abstractions.Tests;

[TestClass]
public sealed class TestingProcessHoldTests
{
    [TestMethod]
    public void GetOrAdd_returns_the_same_instance_for_a_matching_type()
    {
        var first = TestingProcessHold.GetOrAdd("hold-same", static () => new List<int> { 1 });
        var second = TestingProcessHold.GetOrAdd("hold-same", static () => new List<int> { 2 });

        Assert.AreSame(first, second);
        Assert.AreSequenceEqual([1], first);
    }

    [TestMethod]
    public void GetOrAdd_does_not_overwrite_when_the_stored_type_does_not_match()
    {
        var stored = TestingProcessHold.GetOrAdd("hold-mismatch", static () => new List<int> { 1 });
        var fallback = TestingProcessHold.GetOrAdd("hold-mismatch", static () => new List<string> { "x" });
        var again = TestingProcessHold.GetOrAdd("hold-mismatch", static () => new List<int> { 9 });

        Assert.AreSequenceEqual(["x"], fallback);
        Assert.AreSame(stored, again);
        Assert.AreSequenceEqual([1], again);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void GetOrAdd_rejects_blank_keys(string key)
    {
        var exception = Assert.ThrowsExactly<ArgumentException>(() =>
            TestingProcessHold.GetOrAdd(key, static () => new object()));

        Assert.AreEqual("key", exception.ParamName);
    }

    [TestMethod]
    public void GetOrAdd_rejects_null_factory()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            TestingProcessHold.GetOrAdd<object>("hold-null-factory-" + Guid.NewGuid().ToString("N"), null!));
    }

    [TestMethod]
    public void GetOrAdd_rejects_null_factory_result()
    {
        var key = "hold-null-result-" + Guid.NewGuid().ToString("N");
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            TestingProcessHold.GetOrAdd<object>(key, static () => null!));

        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }
}
