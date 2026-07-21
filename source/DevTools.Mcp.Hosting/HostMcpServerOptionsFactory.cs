using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Hosting;

public sealed class HostMcpServerOptionsFactory(
    IHostAppInfo hostInfo,
    McpServerPrimitiveCollection<McpServerTool> tools,
    McpServerPrimitiveCollection<McpServerPrompt> prompts,
    McpServerResourceCollection resources)
{
    public McpServerOptions Create()
    {
        var options = new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = hostInfo.Host.ToString(),
                Version = hostInfo.VersionNumber
            },
            ServerInstructions = "Host-local CAD/BIM automation runtime. Tool calls run in the connected host process.",
            ToolCollection = tools,
            PromptCollection = prompts,
            ResourceCollection = resources,
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability { ListChanged = true },
                Prompts = new PromptsCapability { ListChanged = true },
                Resources = new ResourcesCapability { ListChanged = true }
            }
        };

#pragma warning disable MCPEXP001
        options.TaskStore = new InMemoryMcpTaskStore();
#pragma warning restore MCPEXP001
        return options;
    }
}
