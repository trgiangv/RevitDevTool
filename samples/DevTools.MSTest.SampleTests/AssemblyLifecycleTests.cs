namespace DevTools.MSTest.SampleTests;

// One [AssemblyInitialize] per assembly, plus [GlobalTestInitialize], which runs before every test.

[TestClass]
public sealed class AssemblyLifecycleTests
{
    internal static int AssemblyInitializes;
    internal static int GlobalInitializes;

    [AssemblyInitialize]
    public static void Mark_assembly_started(TestContext context)
    {
        _ = context;
        AssemblyInitializes++;
    }

    [GlobalTestInitialize]
    public static void Mark_global_started(TestContext context)
    {
        _ = context;
        GlobalInitializes++;
    }

    [AssemblyCleanup]
    public static void Mark_assembly_finished(TestContext context)
    {
        _ = context;
        AssemblyInitializes = 0;
    }

    [GlobalTestCleanup]
    public static void Mark_global_finished(TestContext context)
    {
        _ = context;
        GlobalInitializes = 0;
    }

    [TestMethod]
    public void Assembly_and_global_initialize_ran()
    {
        Assert.IsGreaterThan(0, AssemblyInitializes);
        Assert.IsGreaterThan(0, GlobalInitializes);
    }
}
