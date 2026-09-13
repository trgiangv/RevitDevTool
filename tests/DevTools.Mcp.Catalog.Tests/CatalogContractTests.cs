using DevTools.Mcp.Catalog;
using DevTools.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Mcp.Catalog.Tests;

[TestClass]
public sealed class CatalogContractTests
{
    [TestMethod]
    public void AddMcp_RegistersTaskStoreAndServer()
    {
        var services = new ServiceCollection();

        services.AddMcp();
        using var provider = services.BuildServiceProvider();

        Assert.IsNotNull(provider.GetService<ModelContextProtocol.Extensions.Tasks.IMcpTaskStore>());
    }

    [TestMethod]
    public void AddMcpCatalog_RegistersCatalogStoreAndLoader()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Moq.Mock.Of<ISettingsService>());

        services.AddMcpCatalog();
        using var provider = services.BuildServiceProvider();

        Assert.IsNotNull(provider.GetRequiredService<McpCatalogStore>());
        Assert.IsNotNull(provider.GetRequiredService<IMcpCatalogLoader>());
        Assert.IsTrue(provider.GetServices<IMcpRegistryProvider>().Any(registry => registry is DotnetMcpRegistryProvider));
        Assert.IsTrue(provider.GetServices<IMcpRegistryProvider>().Any(registry => registry is BuiltInMcpRegistryProvider));
    }

    [TestMethod]
    public void McpRegistryCatalog_DefaultsAreEmpty()
    {
        var catalog = new McpRegistryCatalog();
        Assert.IsEmpty(catalog.Tools);
        Assert.IsEmpty(catalog.Resources);

        var emptyA = McpRegistryCatalog.Empty;
        var emptyB = McpRegistryCatalog.Empty;
        Assert.AreSame(emptyA, emptyB);
    }

    [TestMethod]
    public void McpPrimitiveBinding_CreatePrimitiveId_NormalizesDisplayNameAndToolId()
    {
        var id = McpPrimitiveBinding.CreatePrimitiveId("Read Walls", "Tools/Wall Tools");
        Assert.AreEqual("Read-Walls_[Tools/Wall-Tools]", id);

        var idWithSpaces = McpPrimitiveBinding.CreatePrimitiveId("read_walls", "sample:read_walls");
        Assert.AreEqual("read_walls_[sample:read_walls]", idWithSpaces);
    }

    [TestMethod]
    public void McpPrimitiveBinding_CreatePrimitiveId_ForResources()
    {
        var resourceId = McpPrimitiveBinding.CreatePrimitiveId(
            "demo_view",
            "sample.dll:McpToolsetDemo.McpSampleResources.DemoView");

        Assert.AreEqual("demo_view_[sample.dll:McpToolsetDemo.McpSampleResources.DemoView]", resourceId);
    }

    [TestMethod]
    public void McpPrimitiveBinding_CreatePrimitiveId_HandlesNullAndEmpty()
    {
        var id = McpPrimitiveBinding.CreatePrimitiveId(null, null);
        Assert.AreEqual("unknown_[unknown]", id);

        var idWithName = McpPrimitiveBinding.CreatePrimitiveId("tool", null);
        Assert.AreEqual("tool_[unknown]", idWithName);
    }
}
