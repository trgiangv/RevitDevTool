using DevTools.Daemon.Desktop;
using DevTools.Daemon.Gateway;
using DevTools.Daemon.Tests.Support;
using DevTools.Ipc;

namespace DevTools.Daemon.Tests;

using System.Linq;

[TestClass]
public sealed class HostInstancesTests
{
    [TestMethod]
    public void Refresh_ListsConnectedAndDiscoveredHosts()
    {
        var connected = DaemonTestDoubles.CreateCatalogEntry("Revit", "2025", 1001);
        var broker = DaemonTestDoubles.CreateHostBroker([connected]);
        var discoveredPipe = HostPipeName.FormatMcp("AutoCad", "2026", 2002);
        var scanner = DaemonTestDoubles.CreatePipeScanner([discoveredPipe]);

        var hosts = new HostInstances(broker.Object, scanner.Object);

        Assert.AreEqual(1, hosts.Count.Value);
        Assert.AreEqual(2, hosts.Rows.Count);
        Assert.Contains(row => row.Pid == 1001 && row.Status == "Connected", hosts.Rows);
        Assert.Contains(row => row.Pid == 2002 && row.Status == "Discovered", hosts.Rows);
    }

    [TestMethod]
    public void Refresh_SkipsDuplicateDiscoveredPid()
    {
        var connected = DaemonTestDoubles.CreateCatalogEntry("Revit", "2025", 1001);
        var broker = DaemonTestDoubles.CreateHostBroker([connected]);
        var discoveredPipe = HostPipeName.FormatMcp("Revit", "2025", 1001);
        var scanner = DaemonTestDoubles.CreatePipeScanner([discoveredPipe]);

        var hosts = new HostInstances(broker.Object, scanner.Object);

        Assert.AreEqual(1, hosts.Count.Value);
        Enumerable.Single(hosts.Rows);
    }

    [TestMethod]
    public void Refresh_PicksUpNewlyConnectedHosts()
    {
        var entries = new List<DevTools.Mcp.Core.Sessions.HostCatalogEntry>();
        var catalog = new Moq.Mock<DevTools.Mcp.Core.Sessions.IConnectedHostCatalog>();
        catalog.Setup(c => c.List()).Returns(() => entries);
        var broker = new Moq.Mock<DevTools.Mcp.Core.Sessions.IHostBroker>();
        broker.Setup(b => b.Catalog).Returns(catalog.Object);
        var scanner = DaemonTestDoubles.CreatePipeScanner();
        var hosts = new HostInstances(broker.Object, scanner.Object);
        Assert.IsEmpty(hosts.Rows);

        entries.Add(DaemonTestDoubles.CreateCatalogEntry("Revit", "2025", 42));
        hosts.Refresh();
        Enumerable.Single(hosts.Rows);
    }
}
