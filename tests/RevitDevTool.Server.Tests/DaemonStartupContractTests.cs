using System.Reflection;
using DevTools.Daemon.Hosting;

namespace RevitDevTool.Server.Tests;

public sealed class DaemonStartupContractTests
{
    [Fact]
    public void WpfEntryPoint_IsStaThread()
    {
        var entryPoint = typeof(DaemonHostBuilder).Assembly.EntryPoint;

        Assert.NotNull(entryPoint);
        Assert.NotNull(entryPoint.GetCustomAttribute<STAThreadAttribute>());
    }
}
