using DevTools.Hosting;
using DevTools.Mcp;
using DevTools.Mcp.Hosting;
using DevTools.Mcp.Isolation;
using DevTools.Mcp.Transport;
using DevTools.Mcp.Adapter.Tests.Harness;
using DevTools.Mcp.Core.Sessions;
using DevTools.Settings;
using DevTools.Settings.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevTools.Mcp.Adapter.Tests;

[TestClass]
public sealed class HostAdapterRegistrationTests
{
    [TestMethod]
    public void AddMcpAdapter_RegistersPipeServer()
    {
        var services = new ServiceCollection();
        services.AddMcpAdapter();

        Assert.IsTrue(services.Any(descriptor => descriptor.ServiceType == typeof(McpPipeServer)));
    }

    [TestMethod]
    public void AddMcpAdapter_ResolvesPipeServerFromRegisteredSources()
    {
        var loader = new Mock<ICatalogLoader>();
        var settings = new Mock<ISettingsService>();
        settings.Setup(s => s.McpRegistryConfig).Returns(new McpRegistryConfig());

        var services = new ServiceCollection();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(loader.Object);
        services.AddSingleton(settings.Object);
        services.AddSingleton<McpCatalogStore>();
        services.AddSingleton<McpAssemblyContextManager>();
        services.AddSingleton(Mock.Of<IConnectTracker>());
        services.AddSingleton(Mock.Of<IHostContextExecutor>());
        services.AddSingleton(Mock.Of<IHostAppInfo>());
        services.AddSingleton<IMcpSource>(_ => Mock.Of<IMcpSource>());
        services.AddMcpAdapter();

        using var provider = services.BuildServiceProvider();
        var server = provider.GetRequiredService<McpPipeServer>();
        Assert.IsNotNull(server);
    }

    [TestMethod]
    public void HostServerOptions_AdvertisesListChanged()
    {
        var (options, _, _, services) = McpHostTestHarness.CreateServerOptionsWithTool("ping");
        using (services)
        {
            Assert.IsTrue(options.Capabilities?.Tools?.ListChanged);
            Assert.IsTrue(options.Capabilities?.Resources?.ListChanged);
            Assert.IsFalse(options.Capabilities?.Resources?.Subscribe);
        }
    }
}
