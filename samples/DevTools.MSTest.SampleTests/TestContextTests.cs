namespace DevTools.MSTest.SampleTests;

// TestContext is the MSTest property-injection surface for the executing test.

[TestClass]
public sealed class TestContextTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Context_names_the_executing_method()
    {
        Assert.AreEqual(nameof(Context_names_the_executing_method), TestContext.TestName);
        Assert.Contains(nameof(TestContextTests), TestContext.FullyQualifiedTestClassName);
    }

    [TestMethod]
    public void Cancellation_token_is_available()
    {
        Assert.IsFalse(TestContext.CancellationToken.IsCancellationRequested);
    }
}
