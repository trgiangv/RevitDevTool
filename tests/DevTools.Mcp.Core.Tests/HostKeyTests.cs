using DevTools.Mcp.Core.Sessions;

namespace DevTools.Mcp.Core.Tests;

[TestClass]
public sealed class HostKeyTests
{
    [TestMethod]
    public void ToString_FormatsMachineAndProcessId()
    {
        var key = new HostKey("machine-a", 12345);
        Assert.AreEqual("machine-a:12345", key.ToString());
    }
}
