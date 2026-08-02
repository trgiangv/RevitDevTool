using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Hosting;

/// <summary>
/// Builds <see cref="McpServerOptions"/> for the external Daemon MCP Server (fixed tool/prompt surface).
/// </summary>
public static class McpServerFactory
{
    public static McpServerOptions CreateOptions(
        McpServerPrimitiveCollection<McpServerTool> toolCollection,
        McpServerPrimitiveCollection<McpServerPrompt> promptCollection,
        IServiceProvider appServices)
    {
        var options = new McpServerOptions
        {
            ToolCollection = toolCollection,
            PromptCollection = promptCollection,
            ResourceCollection = [],
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability { ListChanged = false },
                Prompts = new PromptsCapability { ListChanged = false },
                Resources = new ResourcesCapability { ListChanged = false }
            }
        };

        McpServerConfigurator.Apply(options, appServices);
        return options;
    }
}
