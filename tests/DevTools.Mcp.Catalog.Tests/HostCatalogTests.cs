using System.Text.Json;
using DevTools.Ipc;
using DevTools.Mcp.Client;
using DevTools.Mcp.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Catalog.Tests;

public class HostCatalogTests
{
    [Fact]
    public async Task AddMcpHostClient_ExposesSameBrokerAsDiscoveryService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMcpHostClient();
        await using var provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IHostBroker>(), provider.GetRequiredService<IHostDiscovery>());
    }

    [Fact]
    public void Replace_AtomicallyReplacesEntryForHostKey()
    {
        var catalog = new ConnectedHostCatalog();
        var key = new HostKey("machine-a", 101);

        catalog.Replace(Entry(key, tools: ["old_tool"]));
        catalog.Replace(Entry(key, tools: ["new_tool"]));

        Assert.Equal(HostCatalogResolutionState.NotFound, catalog.Resolve(HostCatalogKind.Tool, "old_tool", key.MachineId, key.ProcessId).State);
        Assert.Equal(HostCatalogResolutionState.Found, catalog.Resolve(HostCatalogKind.Tool, "new_tool", key.MachineId, key.ProcessId).State);
    }

    [Fact]
    public void Search_OrdersHitsByKindThenTargetThenPid()
    {
        var catalog = new ConnectedHostCatalog();
        catalog.Replace(Entry(new HostKey("m", 1), tools: ["execute_csharp_code", "csharp_helper"]));

        var hits = catalog.Search("csharp", [HostCatalogKind.Tool], limit: 10);

        Assert.Equal(2, hits.Count);
        Assert.Equal("csharp_helper", hits[0].Target);
        Assert.Equal("execute_csharp_code", hits[1].Target);
    }

    [Fact]
    public void Search_FiltersByMachineAndPid()
    {
        var catalog = new ConnectedHostCatalog();
        catalog.Replace(Entry(new HostKey("m1", 101), tools: ["shared"]));
        catalog.Replace(Entry(new HostKey("m1", 202), tools: ["shared"]));
        catalog.Replace(Entry(new HostKey("m2", 303), tools: ["shared"]));

        var hits = catalog.Search("shared", machineId: "m1", hostInstanceId: 202);

        Assert.Single(hits);
        Assert.Equal(202, hits[0].Key.ProcessId);
        Assert.Equal("m1", hits[0].Key.MachineId);
    }

    [Fact]
    public void Search_FiltersByKind()
    {
        var catalog = new ConnectedHostCatalog();
        catalog.Replace(Entry(
            new HostKey("m", 1),
            tools: ["execute_csharp_code"],
            resources: ["revit://version"],
            templates: ["revit://element/{id}"]));

        var tools = catalog.Search(null, [HostCatalogKind.Tool]);
        var resources = catalog.Search(null, [HostCatalogKind.Resource]);
        var templates = catalog.Search(null, [HostCatalogKind.ResourceTemplate]);

        Assert.Single(tools);
        Assert.Equal(HostCatalogKind.Tool, tools[0].Kind);
        Assert.Single(resources);
        Assert.Equal(HostCatalogKind.Resource, resources[0].Kind);
        Assert.Single(templates);
        Assert.Equal(HostCatalogKind.ResourceTemplate, templates[0].Kind);
    }

    [Fact]
    public void Resolve_RequiresExplicitInstanceWhenAmbiguous()
    {
        var catalog = new ConnectedHostCatalog();
        catalog.Replace(Entry(new HostKey("m", 101), tools: ["shared"]));
        catalog.Replace(Entry(new HostKey("m", 202), tools: ["shared"]));

        var ambiguous = catalog.Resolve(HostCatalogKind.Tool, "shared", "m", null);
        var found = catalog.Resolve(HostCatalogKind.Tool, "shared", "m", 202);

        Assert.Equal(HostCatalogResolutionState.Ambiguous, ambiguous.State);
        Assert.Equal(2, ambiguous.Candidates.Count);
        Assert.Equal(HostCatalogResolutionState.Found, found.State);
        Assert.Equal(202, found.Hit!.Key.ProcessId);
    }

    [Fact]
    public void Search_RanksExactBeforePrefixBeforeDescription()
    {
        var catalog = new ConnectedHostCatalog();
        catalog.Replace(Entry(
            new HostKey("m", 1),
            tools:
            [
                ("find", "unrelated"),
                ("find_elements", "Find by category"),
                ("revit_list_rooms", "Find elements using walls keyword in description")
            ]));

        var hits = catalog.Search("find", [HostCatalogKind.Tool], limit: 10);

        Assert.Equal(3, hits.Count);
        Assert.Equal("find", hits[0].Target);
        Assert.Equal("find_elements", hits[1].Target);
        Assert.Equal("revit_list_rooms", hits[2].Target);
    }

    [Fact]
    public void Search_DescriptionSubstring_Matches()
    {
        var catalog = new ConnectedHostCatalog();
        catalog.Replace(Entry(
            new HostKey("m", 1),
            tools:
            [
                ("revit_find_elements", "Find elements using structured FilterSpec queries for walls"),
                ("execute_csharp_code", "Execute arbitrary C# in the host")
            ]));

        var hits = catalog.Search("walls", [HostCatalogKind.Tool], limit: 5);

        Assert.Single(hits);
        Assert.Equal("revit_find_elements", hits[0].Target);
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsCatalog()
    {
        var catalog = new ConnectedHostCatalog();
        catalog.Replace(Entry(new HostKey("m", 1), tools: ["execute_csharp_code"]));

        var hits = catalog.Search(null, [HostCatalogKind.Tool]);

        Assert.Single(hits);
        Assert.Equal("execute_csharp_code", hits[0].Target);
    }

    [Fact]
    public void Remove_InvalidatesHostOnDisconnect()
    {
        var catalog = new ConnectedHostCatalog();
        var key = new HostKey("m", 101);
        catalog.Replace(Entry(key, tools: ["tool"]));

        Assert.True(catalog.Remove(key));
        Assert.Empty(catalog.List());
        Assert.Equal(HostCatalogResolutionState.NotFound, catalog.Resolve(HostCatalogKind.Tool, "tool", "m", 101).State);
    }

    private static HostCatalogEntry Entry(
        HostKey key,
        string[]? tools = null,
        string[]? resources = null,
        string[]? templates = null) =>
        Entry(key, (tools ?? []).Select(name => (name, $"{name} description")).ToArray(), resources, templates);

    private static HostCatalogEntry Entry(
        HostKey key,
        (string Name, string Description)[] tools,
        string[]? resources = null,
        string[]? templates = null) =>
        new()
        {
            Key = key,
            Instance = new InstanceInfo
            {
                ProcessId = key.ProcessId,
                HostApp = "Revit",
                VersionNumber = "2025"
            },
            PipeName = $"DevToolsMcp_Revit_2025_{key.ProcessId}",
            Tools = tools.Select(pair => new Tool
            {
                Name = pair.Name,
                Description = pair.Description,
                InputSchema = JsonSerializer.SerializeToElement(new { type = "object" })
            }).ToArray(),
            Resources = (resources ?? []).Select(uri => new Resource
            {
                Uri = uri,
                Name = uri,
                Description = $"{uri} description"
            }).ToArray(),
            ResourceTemplates = (templates ?? []).Select(uri => new ResourceTemplate
            {
                UriTemplate = uri,
                Name = uri,
                Description = $"{uri} description"
            }).ToArray()
        };
}
