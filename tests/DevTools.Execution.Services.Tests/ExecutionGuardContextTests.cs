using DevTools.Execution.Abstractions;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class ExecutionGuardContextTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Mode_DefaultsToPassthrough()
    {
        Assert.AreEqual(ExecutionGuardMode.Passthrough, ExecutionGuardContext.Mode);
    }

    [TestMethod]
    public void RollbackSummary_DefaultsToNull()
    {
        Assert.IsNull(ExecutionGuardContext.RollbackSummary);
    }

    [TestMethod]
    public void Mode_SetAndGet_RoundTrips()
    {
        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
        Assert.AreEqual(ExecutionGuardMode.Suppress, ExecutionGuardContext.Mode);

        ExecutionGuardContext.Mode = ExecutionGuardMode.Passthrough;
        Assert.AreEqual(ExecutionGuardMode.Passthrough, ExecutionGuardContext.Mode);
    }

    [TestMethod]
    public async Task Mode_IsIsolatedPerAsyncFlow()
    {
        ExecutionGuardContext.Mode = ExecutionGuardMode.Passthrough;

        var innerMode = ExecutionGuardMode.Passthrough;

        await Task.Run(() =>
        {
            ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
            innerMode = ExecutionGuardContext.Mode;
        }, TestContext.CancellationToken);

        Assert.AreEqual(ExecutionGuardMode.Suppress, innerMode);
        Assert.AreEqual(ExecutionGuardMode.Passthrough, ExecutionGuardContext.Mode);
    }

    [TestMethod]
    public void RollbackSummary_SetAndGet_RoundTrips()
    {
        ExecutionGuardContext.RollbackSummary = "rolled back: test failure";
        Assert.AreEqual("rolled back: test failure", ExecutionGuardContext.RollbackSummary);
        ExecutionGuardContext.RollbackSummary = null;
    }

    [TestMethod]
    public async Task RollbackSummary_IsIsolatedPerAsyncFlow()
    {
        ExecutionGuardContext.RollbackSummary = null;

        string? innerSummary = null;

        await Task.Run(() =>
        {
            ExecutionGuardContext.RollbackSummary = "inner rollback";
            innerSummary = ExecutionGuardContext.RollbackSummary;
        }, TestContext.CancellationToken);

        Assert.AreEqual("inner rollback", innerSummary);
        Assert.IsNull(ExecutionGuardContext.RollbackSummary);
    }
}
