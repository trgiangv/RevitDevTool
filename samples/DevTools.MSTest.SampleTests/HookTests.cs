namespace DevTools.MSTest.SampleTests;

// Class and test initialize/cleanup, the MSTest counterpart of TUnit hooks.

[TestClass]
public sealed class HookTests
{
    private static int _classRuns;
    private bool _ready;

    [ClassInitialize]
    public static void Mark_class_started(TestContext context)
    {
        _ = context;
        _classRuns++;
    }

    [TestInitialize]
    public void Mark_ready() => _ready = true;

    [TestCleanup]
    public void Clear_ready() => _ready = false;

    [ClassCleanup]
    public static void Mark_class_finished() => _classRuns = 0;

    [TestMethod]
    public void Before_test_hook_ran()
    {
        Assert.IsTrue(_ready);
    }

    [TestMethod]
    public void Class_hook_ran_at_least_once()
    {
        Assert.IsTrue(_classRuns > 0);
    }
}
