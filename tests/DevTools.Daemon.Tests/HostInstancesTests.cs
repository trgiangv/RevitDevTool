using DevTools.Daemon.Desktop;
using DevTools.Daemon.Mcp.Processes;
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
        var broker = DaemonTestDoubles.CreateProcessSessions([connected]);
        var discoveredPipe = HostPipeName.FormatMcp("AutoCad", "2026", 2002);
        var scanner = DaemonTestDoubles.CreatePipeScanner([discoveredPipe]);

        var hosts = new HostInstances(broker.Object, scanner.Object);

        Assert.AreEqual(1, hosts.Count);
        Assert.AreEqual(2, hosts.Rows.Count);
        Assert.Contains(row => row.Pid == 1001 && row.Status == "Connected", hosts.Rows);
        Assert.Contains(row => row.Pid == 2002 && row.Status == "Discovered", hosts.Rows);
    }

    [TestMethod]
    public void Refresh_SkipsDuplicateDiscoveredPid()
    {
        var connected = DaemonTestDoubles.CreateCatalogEntry("Revit", "2025", 1001);
        var broker = DaemonTestDoubles.CreateProcessSessions([connected]);
        var discoveredPipe = HostPipeName.FormatMcp("Revit", "2025", 1001);
        var scanner = DaemonTestDoubles.CreatePipeScanner([discoveredPipe]);

        var hosts = new HostInstances(broker.Object, scanner.Object);

        Assert.AreEqual(1, hosts.Count);
        Enumerable.Single(hosts.Rows);
    }

    [TestMethod]
    public void Refresh_PicksUpNewlyConnectedHosts()
    {
        var catalog = new ProcessCatalogs();
        var broker = new Moq.Mock<IProcessSessions>();
        broker.Setup(b => b.Catalog).Returns(catalog);
        var scanner = DaemonTestDoubles.CreatePipeScanner();
        var hosts = new HostInstances(broker.Object, scanner.Object);
        Assert.IsEmpty(hosts.Rows);

        catalog.Replace(DaemonTestDoubles.CreateCatalogEntry("Revit", "2025", 42));
        hosts.Refresh();
        Enumerable.Single(hosts.Rows);
    }
}
