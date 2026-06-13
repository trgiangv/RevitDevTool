using DevTools.McpServer;
using ModelContextProtocol.Server;

namespace RevitDevTool.Server.Tests;

public class McpServerConfigurationTests
{
    [Fact]
    public void ConfigureDynamicCatalog_AdvertisesListChangedCapabilities()
    {
        var options = new McpServerOptions();

        options.ConfigureDynamicCatalog();

        Assert.True(options.Capabilities?.Tools?.ListChanged);
        Assert.True(options.Capabilities?.Prompts?.ListChanged);
        Assert.True(options.Capabilities?.Resources?.ListChanged);
    }
}
