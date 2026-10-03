using DevTools.Mcp;
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
        Assert.IsNotNull(provider.GetRequiredService<ICatalogLoader>());
        Assert.IsTrue(provider.GetServices<IRegistryProvider>().Any(registry => registry is DotnetMcpRegistryProvider));
        Assert.IsTrue(provider.GetServices<IRegistryProvider>().Any(registry => registry is BuiltInMcpRegistryProvider));
    }

    [TestMethod]
    public void RegistryCatalog_DefaultsAreEmpty()
    {
        var catalog = new RegistryCatalog();
        Assert.IsEmpty(catalog.Tools);
        Assert.IsEmpty(catalog.Resources);

        var emptyA = RegistryCatalog.Empty;
        var emptyB = RegistryCatalog.Empty;
        Assert.AreSame(emptyA, emptyB);
    }

    [TestMethod]
    public void PrimitiveBinding_CreatePrimitiveId_KeepsNameAndAddress()
    {
        var id = PrimitiveBinding.CreatePrimitiveId("Read Walls", "Tools/Wall Tools");
        Assert.AreEqual("Read Walls_[Tools/Wall Tools]", id);

        var idWithSpaces = PrimitiveBinding.CreatePrimitiveId("read_walls", "sample:read_walls");
        Assert.AreEqual("read_walls_[sample:read_walls]", idWithSpaces);
    }

    [TestMethod]
    public void PrimitiveBinding_CreatePrimitiveId_ForResources()
    {
        var resourceId = PrimitiveBinding.CreatePrimitiveId(
            "demo_view",
            "sample.dll:McpToolsetDemo.McpSampleResources.DemoView");

        Assert.AreEqual("demo_view_[sample.dll:McpToolsetDemo.McpSampleResources.DemoView]", resourceId);
    }

    [TestMethod]
    public void PrimitiveBinding_CreatePrimitiveId_HandlesNullAndEmpty()
    {
        var id = PrimitiveBinding.CreatePrimitiveId(null, null);
        Assert.AreEqual("_[]", id);

        var idWithName = PrimitiveBinding.CreatePrimitiveId("tool", null);
        Assert.AreEqual("tool_[]", idWithName);
    }
}
