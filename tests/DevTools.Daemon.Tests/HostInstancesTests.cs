using DevTools.Daemon.Desktop;
using DevTools.Daemon.Gateway;
using DevTools.Daemon.Tests.Support;
using DevTools.Ipc;

namespace DevTools.Daemon.Tests;

public sealed class HostInstancesTests
{
    [Fact]
    public void Refresh_ListsConnectedAndDiscoveredHosts()
    {
        var connected = DaemonTestDoubles.CreateCatalogEntry("Revit", "2025", 1001);
        var broker = DaemonTestDoubles.CreateHostBroker([connected]);
        var discoveredPipe = HostPipeName.FormatMcp("AutoCad", "2026", 2002);
        var scanner = DaemonTestDoubles.CreatePipeScanner([discoveredPipe]);

        var hosts = new HostInstances(broker.Object, scanner.Object);

        Assert.Equal(1, hosts.Count.Value);
        Assert.Equal(2, hosts.Rows.Count);
        Assert.Contains(hosts.Rows, row => row.Pid == 1001 && row.Status == "Connected");
        Assert.Contains(hosts.Rows, row => row.Pid == 2002 && row.Status == "Discovered");
    }

    [Fact]
    public void Refresh_SkipsDuplicateDiscoveredPid()
    {
        var connected = DaemonTestDoubles.CreateCatalogEntry("Revit", "2025", 1001);
        var broker = DaemonTestDoubles.CreateHostBroker([connected]);
        var discoveredPipe = HostPipeName.FormatMcp("Revit", "2025", 1001);
        var scanner = DaemonTestDoubles.CreatePipeScanner([discoveredPipe]);

        var hosts = new HostInstances(broker.Object, scanner.Object);

        Assert.Equal(1, hosts.Count.Value);
        Assert.Single(hosts.Rows);
    }

    [Fact]
    public void Refresh_PicksUpNewlyConnectedHosts()
    {
        var entries = new List<DevTools.Mcp.Core.Sessions.HostCatalogEntry>();
        var catalog = new Moq.Mock<DevTools.Mcp.Core.Sessions.IConnectedHostCatalog>();
        catalog.Setup(c => c.List()).Returns(() => entries);
        var broker = new Moq.Mock<DevTools.Mcp.Core.Sessions.IHostBroker>();
        broker.Setup(b => b.Catalog).Returns(catalog.Object);
        var scanner = DaemonTestDoubles.CreatePipeScanner();
        var hosts = new HostInstances(broker.Object, scanner.Object);
        Assert.Empty(hosts.Rows);

        entries.Add(DaemonTestDoubles.CreateCatalogEntry("Revit", "2025", 42));
        hosts.Refresh();
        Assert.Single(hosts.Rows);
    }
}
