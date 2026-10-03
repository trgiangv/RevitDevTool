using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Mcp.Search;
using DevTools.Ipc;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class ProcessCatalogsTests
{
    [TestMethod]
    public void ReplaceRemoveClear_ManageCatalogs()
    {
        var catalogs = new ProcessCatalogs();
        var catalog = CreateCatalog(100, "ping", "sample://demo/status");

        catalogs.Replace(catalog);
        Assert.HasCount(1, catalogs.List());

        Assert.IsTrue(catalogs.Remove(100));
        Assert.IsEmpty(catalogs.List());

        catalogs.Replace(catalog);
        catalogs.Clear();
        Assert.IsEmpty(catalogs.List());
    }

    [TestMethod]
    public void Search_WithQuery_UsesFieldScoreAndHalfTokenCutoff()
    {
        var catalog = new ProcessCatalogs();
        catalog.Replace(CreateCatalog(100, "read_file_info", "sample://a", description: "metadata reader"));
        catalog.Replace(CreateCatalog(200, "launch_host", "sample://b", description: "start host"));

        var strong = catalog.Search("read_file_info");
        Assert.HasCount(1, strong);
        Assert.IsGreaterThan(0, strong[0].Score);

        var weak = catalog.Search("zzz aaa");
        Assert.IsEmpty(weak);
    }

    [TestMethod]
    public void Search_FiltersByProcessId()
    {
        var catalog = new ProcessCatalogs();
        catalog.Replace(CreateCatalog(100, "ping", "sample://a"));
        catalog.Replace(CreateCatalog(200, "ping", "sample://b"));

        var matches = catalog.Search(null, processId: 100);

        Assert.AreEqual(3, matches.Count);
        foreach (var match in matches)
            Assert.AreEqual(100, match.ProcessId);
    }

    [TestMethod]
    public void Find_ReturnsTheItemForThatProcess()
    {
        var catalog = new ProcessCatalogs();
        catalog.Replace(CreateCatalog(100, "ping", "sample://a/ping"));
        catalog.Replace(CreateCatalog(200, "ping", "sample://b/ping"));

        var notFound = catalog.Find(CatalogType.Tool, "missing", 100);
        Assert.IsNull(notFound);

        var found = catalog.Find(CatalogType.Resource, "sample://a/ping", 100);
        Assert.IsNotNull(found);
        Assert.AreEqual("sample://a/ping", found.Target);

        var otherProcess = catalog.Find(CatalogType.Tool, "ping", 100);
        Assert.AreEqual(100, otherProcess!.ProcessId);
    }

    private static ProcessCatalog CreateCatalog(int processId, string toolName, string resourceUri, string? description = null) => new()
    {
        ProcessId = processId,
        Instance = new InstanceInfo { HostApp = "Revit", ProcessId = processId, VersionNumber = "2025" },
        PipeName = HostPipeName.FormatMcp("Revit", "2025", processId),
        Tools =
        [
            new Tool
            {
                Name = toolName,
                Description = description ?? $"{toolName} description",
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
