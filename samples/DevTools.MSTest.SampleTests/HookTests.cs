namespace DevTools.MSTest.SampleTests;

// Class and test initialize/cleanup, the MSTest counterpart of TUnit hooks.

[TestClass]
public sealed class HookTests
{
    private static int ClassRuns;
    private bool _ready;

    [ClassInitialize]
    public static void Mark_class_started(TestContext context)
    {
        _ = context;
        ClassRuns++;
    }

    [TestInitialize]
    public void Mark_ready() => _ready = true;

    [TestCleanup]
    public void Clear_ready() => _ready = false;

    [ClassCleanup]
    public static void Mark_class_finished() => ClassRuns = 0;

    [TestMethod]
    public void Before_test_hook_ran()
    {
        Assert.IsTrue(_ready);
    }

    [TestMethod]
    public void Class_hook_ran_at_least_once()
    {
        Assert.IsTrue(ClassRuns > 0);
    }
}
