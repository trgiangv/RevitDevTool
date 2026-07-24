using DevTools.Execution.Abstractions;

namespace DevTools.Execution.Tests;

/// <summary>
/// Unit tests for <see cref="ExecutionGuardContext"/> ambient context behavior.
/// Verifies AsyncLocal isolation and default values.
/// </summary>
public sealed class ExecutionGuardContextTests
{
    [Fact]
    public void Mode_DefaultsToPassthrough()
    {
        Assert.Equal(ExecutionGuardMode.Passthrough, ExecutionGuardContext.Mode);
    }

    [Fact]
    public void RollbackSummary_DefaultsToNull()
    {
        Assert.Null(ExecutionGuardContext.RollbackSummary);
    }

    [Fact]
    public void Mode_SetAndGet_RoundTrips()
    {
        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
        Assert.Equal(ExecutionGuardMode.Suppress, ExecutionGuardContext.Mode);

        ExecutionGuardContext.Mode = ExecutionGuardMode.Passthrough;
        Assert.Equal(ExecutionGuardMode.Passthrough, ExecutionGuardContext.Mode);
    }

    [Fact]
    public async Task Mode_IsIsolatedPerAsyncFlow()
    {
        ExecutionGuardContext.Mode = ExecutionGuardMode.Passthrough;

        var innerMode = ExecutionGuardMode.Passthrough;

        await Task.Run(() =>
        {
            ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
            innerMode = ExecutionGuardContext.Mode;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(ExecutionGuardMode.Suppress, innerMode);
        Assert.Equal(ExecutionGuardMode.Passthrough, ExecutionGuardContext.Mode);
    }

    [Fact]
    public void RollbackSummary_SetAndGet_RoundTrips()
    {
        ExecutionGuardContext.RollbackSummary = "rolled back: test failure";
        Assert.Equal("rolled back: test failure", ExecutionGuardContext.RollbackSummary);
        ExecutionGuardContext.RollbackSummary = null;
    }

    [Fact]
    public async Task RollbackSummary_IsIsolatedPerAsyncFlow()
    {
        ExecutionGuardContext.RollbackSummary = null;

        string? innerSummary = null;

        await Task.Run(() =>
        {
            ExecutionGuardContext.RollbackSummary = "inner rollback";
            innerSummary = ExecutionGuardContext.RollbackSummary;
        }, TestContext.Current.CancellationToken);

        Assert.Equal("inner rollback", innerSummary);
        Assert.Null(ExecutionGuardContext.RollbackSummary);
    }
}
