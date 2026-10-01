using System.Runtime.InteropServices;

namespace DevTools.MSTest.SampleTests;

// [OSCondition] includes or skips a test before it runs. Linux-only stays listed on Windows.

[TestClass]
public sealed class ConditionTests
{
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Linux)]
    public void Non_linux_host_is_included()
    {
        Assert.IsTrue(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Linux)]
    public void Linux_only_is_listed_not_run()
    {
        Assert.Fail("A Windows host must not execute a Linux-only test.");
    }
}
