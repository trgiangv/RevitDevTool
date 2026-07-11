using System.Text.Json;
using DevTools.Mcp.Routing.Catalog;
using ModelContextProtocol.Protocol;

namespace RevitDevTool.Server.Tests;

public class DynamicToolCatalogTests
{
    [Fact]
    public void ReplaceSnapshot_PreservesSameToolAcrossInstances()
    {
        var catalog = new DynamicToolCatalog();

        catalog.ReplaceSnapshot([
            Registration("shared_tool", 101, "Revit"),
            Registration("shared_tool", 202, "AutoCad")
        ]);

        var registrations = catalog.List();
        Assert.Equal(2, registrations.Count);
        Assert.Contains(registrations, item => item.Instance.ProcessId == 101);
        Assert.Contains(registrations, item => item.Instance.ProcessId == 202);
    }

    [Fact]
    public void Resolve_RequiresInstanceWhenMultipleInstancesProvideTool()
    {
        var catalog = new DynamicToolCatalog();
        catalog.ReplaceSnapshot([
            Registration("shared_tool", 101, "Revit"),
            Registration("shared_tool", 202, "Revit")
        ]);

        var result = catalog.Resolve("shared_tool", null);

        Assert.Equal(DynamicToolResolutionState.Ambiguous, result.State);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void Resolve_SelectsExplicitInstance()
    {
        var catalog = new DynamicToolCatalog();
        catalog.ReplaceSnapshot([
            Registration("shared_tool", 101, "Revit"),
            Registration("shared_tool", 202, "AutoCad")
        ]);

        var result = catalog.Resolve("shared_tool", 202);

        Assert.Equal(DynamicToolResolutionState.Found, result.State);
        Assert.Equal("AutoCad", result.Registration?.Instance.HostApp);
    }

    [Fact]
    public void ReplaceSnapshot_RemovesRegistrationsNoLongerReported()
    {
        var catalog = new DynamicToolCatalog();
        catalog.ReplaceSnapshot([Registration("old_tool", 101, "Revit")]);

        catalog.ReplaceSnapshot([Registration("new_tool", 101, "Revit")]);

        Assert.Equal(DynamicToolResolutionState.NotFound, catalog.Resolve("old_tool", 101).State);
        Assert.Equal(DynamicToolResolutionState.Found, catalog.Resolve("new_tool", 101).State);
    }

    private static DynamicToolCatalogEntry Registration(string name, int processId, string hostApp) =>
        new(
            new Tool
            {
                Name = name,
                Description = $"{name} description",
                InputSchema = JsonSerializer.SerializeToElement(new { type = "object" })
            },
            new InstanceInfo
            {
                ProcessId = processId,
                HostApp = hostApp,
                VersionNumber = "2025"
            },
            $"{hostApp}_2025_{processId}");
}
