using DevTools.Mcp.Core.Sessions;

namespace DevTools.Mcp.Core.Tests;

public sealed class HostKeyTests
{
    [Fact]
    public void ToString_FormatsMachineAndProcessId()
    {
        var key = new HostKey("machine-a", 12345);
        Assert.Equal("machine-a:12345", key.ToString());
    }
}
