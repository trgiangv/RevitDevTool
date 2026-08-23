using DevTools.Mcp.Tests.Harness;
using DevTools.Settings;
using DevTools.Settings.Configs;
using Moq;

namespace DevTools.Mcp.Tests;

public sealed class McpCatalogStoreTests
{
    [Fact]
    public async Task ReloadAsync_DoesNotRaiseCatalogChanged_WhenIdsUnchanged()
    {
        var catalog = Catalog(Tool("execute_csharp_code"));
        var store = CreateStore(() => catalog);
        var raised = 0;
        store.CatalogChanged += (_, _) => raised++;

        store.EnsureLoaded();
        await store.ReloadAsync();

        Assert.Equal(0, raised);
        Assert.Single(store.RegisteredTools);
    }

    [Fact]
    public async Task ReloadAsync_RaisesCatalogChanged_WhenNewToolIdAppears()
    {
        var tools = new List<McpRegisteredTool> { Tool("execute_csharp_code") };
        var store = CreateStore(() => Catalog(tools.ToArray()));
        var raised = 0;
        store.CatalogChanged += (_, _) => raised++;

        store.EnsureLoaded();
        tools.Add(Tool("execute_python_code"));
        await store.ReloadAsync();

        Assert.Equal(1, raised);
        Assert.Equal(2, store.RegisteredTools.Count);
    }

    [Fact]
    public async Task ReloadAsync_RaisesCatalogChanged_WhenToolIdRemoved()
    {
        var tools = new List<McpRegisteredTool> { Tool("a"), Tool("b") };
        var store = CreateStore(() => Catalog(tools.ToArray()));
        var raised = 0;
        store.CatalogChanged += (_, _) => raised++;

        store.EnsureLoaded();
        tools.RemoveAt(1);
        await store.ReloadAsync();

        Assert.Equal(1, raised);
        Assert.Single(store.RegisteredTools);
    }

    private static McpCatalogStore CreateStore(Func<McpRegistryCatalog> catalogFactory)
    {
        var loader = new Mock<IMcpCatalogLoader>();
        loader
            .Setup(l => l.LoadCatalog(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(catalogFactory);

        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.McpRegistryConfig).Returns(new McpRegistryConfig());
        return new McpCatalogStore(loader.Object, settings.Object);
    }

    private static McpRegistryCatalog Catalog(params McpRegisteredTool[] tools) => new()
    {
        Tools = tools,
        Resources = [],
    };

    private static McpRegisteredTool Tool(string name) => McpHostTestHarness.CreateRegisteredTool(name);
}
