using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Tests;

public class McpServerConfigurationTests
{
    [Fact]
    public void ConfigureDynamicCatalog_AdvertisesListChangedCapabilities()
    {
        var options = new McpServerOptions();

        ConfigureDynamicCatalog(options);

        Assert.True(options.Capabilities?.Tools?.ListChanged);
        Assert.True(options.Capabilities?.Prompts?.ListChanged);
        Assert.True(options.Capabilities?.Resources?.ListChanged);
    }

    private static void ConfigureDynamicCatalog(McpServerOptions options)
    {
        options.Capabilities ??= new ServerCapabilities();
        options.Capabilities.Tools ??= new ToolsCapability();
        options.Capabilities.Prompts ??= new PromptsCapability();
        options.Capabilities.Resources ??= new ResourcesCapability();

        options.Capabilities.Tools.ListChanged = true;
        options.Capabilities.Prompts.ListChanged = true;
        options.Capabilities.Resources.ListChanged = true;
    }
}
