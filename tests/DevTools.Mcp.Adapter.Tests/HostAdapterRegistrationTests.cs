using DevTools.Mcp;
using DevTools.Mcp.Transport;
using DevTools.Mcp.Adapter.Tests.Harness;
using Microsoft.Extensions.DependencyInjection;

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
