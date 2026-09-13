using DevTools.Ipc;
using DevTools.Mcp.Client;
using DevTools.Mcp.Core.Sessions;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class ConnectedHostCatalogTests
{
    private static readonly HostKey MachineA = new("machine-a", 100);
    private static readonly HostKey MachineB = new("machine-b", 200);

    [TestMethod]
    public void ReplaceRemoveClear_ManageEntries()
    {
        var catalog = new ConnectedHostCatalog();
        var entry = CreateEntry(MachineA, "ping", "sample://demo/status");

        catalog.Replace(entry);
        Assert.HasCount(1, catalog.List());

        Assert.IsTrue(catalog.Remove(MachineA));
        Assert.IsEmpty(catalog.List());

        catalog.Replace(entry);
        catalog.Clear();
        Assert.IsEmpty(catalog.List());
    }

    [TestMethod]
    public void Search_WithoutQuery_ReturnsSortedHits()
    {
        var catalog = new ConnectedHostCatalog();
        catalog.Replace(CreateEntry(MachineA, "zebra_tool", "sample://z"));
        catalog.Replace(CreateEntry(MachineB, "alpha_tool", "sample://a"));

        var hits = catalog.Search(null);

        Assert.AreEqual(6, hits.Count);
        Assert.AreEqual("alpha_tool", hits.First(hit => hit.Kind == HostCatalogKind.Tool && hit.Key == MachineB).Target);
        Assert.AreEqual("zebra_tool", hits.First(hit => hit.Kind == HostCatalogKind.Tool && hit.Key == MachineA).Target);
    }

    [TestMethod]
    public void Search_WithQuery_RanksExactTargetFirst()
    {
        var catalog = new ConnectedHostCatalog();
        catalog.Replace(CreateEntry(MachineA, "read_file_info", "Read CAD metadata"));
        catalog.Replace(CreateEntry(MachineB, "launch_host", "Launch a host"));

        var hits = catalog.Search("read_file_info");

        Assert.HasCount(1, hits);
        Assert.AreEqual(HostCatalogKind.Tool, hits[0].Kind);
        Assert.AreEqual("read_file_info", hits[0].Target);
    }

    [TestMethod]
    public void Search_FiltersByMachineAndProcess()
    {
        var catalog = new ConnectedHostCatalog();
        catalog.Replace(CreateEntry(MachineA, "ping", "sample://a"));
        catalog.Replace(CreateEntry(MachineB, "ping", "sample://b"));

        var hits = catalog.Search(null, machineId: "machine-a", hostInstanceId: 100);

        Assert.AreEqual(3, hits.Count);
        foreach (var hit in hits)
            Assert.AreEqual(MachineA, hit.Key);
    }

    [TestMethod]
    public void Resolve_FoundNotFoundAndAmbiguous()
    {
        var catalog = new ConnectedHostCatalog();
        catalog.Replace(CreateEntry(MachineA, "ping", "sample://a/ping"));
        catalog.Replace(CreateEntry(MachineB, "ping", "sample://b/ping"));

        var notFound = catalog.Resolve(HostCatalogKind.Tool, "missing", null, null);
        Assert.AreEqual(HostCatalogResolutionState.NotFound, notFound.State);
        Assert.IsNull(notFound.Hit);

        var found = catalog.Resolve(HostCatalogKind.Resource, "sample://a/ping", "machine-a", 100);
        Assert.AreEqual(HostCatalogResolutionState.Found, found.State);
        Assert.IsNotNull(found.Hit);

        var ambiguous = catalog.Resolve(HostCatalogKind.Tool, "ping", null, null);
        Assert.AreEqual(HostCatalogResolutionState.Ambiguous, ambiguous.State);
        Assert.AreEqual(2, ambiguous.Candidates.Count);
    }

    private static HostCatalogEntry CreateEntry(HostKey key, string toolName, string resourceUri) => new()
    {
        Key = key,
        Instance = new InstanceInfo { HostApp = "Revit", ProcessId = key.ProcessId, VersionNumber = "2025" },
        PipeName = HostPipeName.FormatMcp("Revit", "2025", key.ProcessId),
        Tools =
        [
            new Tool
            {
                Name = toolName,
                Description = $"{toolName} description",
                InputSchema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }),
            },
        ],
        Resources =
        [
            new Resource
            {
                Name = "demo_resource",
                Uri = resourceUri,
                Description = "Demo resource",
            },
        ],
        ResourceTemplates =
        [
            new ResourceTemplate
            {
                Name = "demo_template",
                UriTemplate = "sample://{id}",
                Description = "Demo template",
            },
        ],
    };
}
