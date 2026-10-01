namespace DevTools.MSTest.SampleTests;

using System.Diagnostics;

// Async Task, ValueTask, and [Ignore]. Ignored tests stay listed and must not run.

[TestClass]
public sealed class LifecycleTests
{
    [TestMethod]
    public async Task Async_delay_then_pass()
    {
        var name = Process.GetCurrentProcess().ProcessName;
        await Task.Delay(1);
        Assert.IsFalse(string.IsNullOrEmpty(name));
    }

    [TestMethod]
    public async ValueTask Async_value_task_completes()
    {
        var length = await new ValueTask<int>(Process.GetCurrentProcess().ProcessName.Length);
        Assert.IsGreaterThan(0, length);
    }

    [Ignore("Listed; must not execute.")]
    [TestMethod]
    public void Ignored_is_listed()
    {
        Assert.Fail("Ignored tests must not execute.");
    }
}
