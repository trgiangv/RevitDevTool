namespace DevTools.MSTest.SampleTests;

using System.Diagnostics;

[TestClass]
public sealed class TimeoutTests
{
    [Timeout(5000)]
    [TestMethod]
    public async Task Completes_within_timeout()
    {
        var name = Process.GetCurrentProcess().ProcessName;
        await Task.Delay(1);
        Assert.IsFalse(string.IsNullOrEmpty(name));
    }
}
