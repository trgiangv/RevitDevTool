using DevTools.Mcp.Catalog;
using DevTools.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Mcp.Catalog.Tests;

public sealed class CatalogContractTests
{
    [Fact]
    public void AddMcp_RegistersTaskStoreAndServer()
    {
        var services = new ServiceCollection();

        services.AddMcp();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<ModelContextProtocol.Extensions.Tasks.IMcpTaskStore>());
    }

    [Fact]
    public void AddMcpCatalog_RegistersCatalogStoreAndLoader()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Moq.Mock.Of<ISettingsService>());

        services.AddMcpCatalog();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<McpCatalogStore>());
        Assert.NotNull(provider.GetRequiredService<IMcpCatalogLoader>());
        Assert.Contains(provider.GetServices<IMcpRegistryProvider>(), registry => registry is DotnetMcpRegistryProvider);
        Assert.Contains(provider.GetServices<IMcpRegistryProvider>(), registry => registry is BuiltInMcpRegistryProvider);
    }

    [Fact]
    public void McpRegistryCatalog_DefaultsAreEmpty()
    {
        var catalog = new McpRegistryCatalog();
        Assert.Empty(catalog.Tools);
        Assert.Empty(catalog.Resources);

        Assert.Same(McpRegistryCatalog.Empty, McpRegistryCatalog.Empty);
    }

    [Fact]
    public void McpPrimitiveBinding_CreatePrimitiveId_NormalizesDisplayNameAndToolId()
    {
        var id = McpPrimitiveBinding.CreatePrimitiveId("Read Walls", "Tools/Wall Tools");
        Assert.Equal("Read-Walls_[Tools/Wall-Tools]", id);

        var idWithSpaces = McpPrimitiveBinding.CreatePrimitiveId("read_walls", "sample:read_walls");
        Assert.Equal("read_walls_[sample:read_walls]", idWithSpaces);
    }

    [Fact]
    public void McpPrimitiveBinding_CreatePrimitiveId_ForResources()
    {
        var resourceId = McpPrimitiveBinding.CreatePrimitiveId(
            "demo_view",
            "sample.dll:McpToolsetDemo.McpSampleResources.DemoView");

        Assert.Equal("demo_view_[sample.dll:McpToolsetDemo.McpSampleResources.DemoView]", resourceId);
    }

    [Fact]
    public void McpPrimitiveBinding_CreatePrimitiveId_HandlesNullAndEmpty()
    {
        var id = McpPrimitiveBinding.CreatePrimitiveId(null, null);
        Assert.Equal("unknown_[unknown]", id);

        var idWithName = McpPrimitiveBinding.CreatePrimitiveId("tool", null);
        Assert.Equal("tool_[unknown]", idWithName);
    }
}
