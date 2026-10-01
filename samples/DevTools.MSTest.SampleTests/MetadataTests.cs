namespace DevTools.MSTest.SampleTests;

// Traits that MSTest copies onto the test: category, description, owner, priority, work items.

[TestClass]
public sealed class MetadataTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("host")]
    [Description("In-host metadata bag")]
    [Owner("devtools")]
    [Priority(2)]
    [WorkItem(4401)]
    [GitHubWorkItem("https://github.com/microsoft/testfx/issues/1")]
    [TestProperty("area", "host")]
    public void Properties_round_trip_through_test_context()
    {
        Assert.AreEqual(nameof(Properties_round_trip_through_test_context), TestContext.TestName);
        Assert.AreEqual("In-host metadata bag", TestContext.Properties["Description"]);
        Assert.AreEqual("devtools", TestContext.Properties["Owner"]);
        Assert.AreEqual("2", TestContext.Properties["Priority"]);
        Assert.AreEqual("host", TestContext.Properties["area"]);
    }
}
