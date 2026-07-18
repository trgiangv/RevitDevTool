using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Routing;

public static class ToolHelpers
{
    public static JsonSerializerOptions IndentedJsonOptions { get; } = new()
    {
        WriteIndented = true
    };

    public static CallToolResult ErrorResult(string message) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = message }] };

    private static void ConfigureDynamicCatalog(this McpServerOptions options)
    {
        options.Capabilities ??= new ServerCapabilities();
        options.Capabilities.Tools ??= new ToolsCapability();
        options.Capabilities.Prompts ??= new PromptsCapability();
        options.Capabilities.Resources ??= new ResourcesCapability();

        options.Capabilities.Tools.ListChanged = true;
        options.Capabilities.Prompts.ListChanged = true;
        options.Capabilities.Resources.ListChanged = true;
    }

    public static McpServerOptions ConfigureGatewayOptions(
        McpServerPrimitiveCollection<McpServerTool> toolCollection,
        McpServerPrimitiveCollection<McpServerPrompt> promptCollection,
        McpServerResourceCollection resourceCollection)
    {
        var options = new McpServerOptions
        {
            ToolCollection = toolCollection,
            PromptCollection = promptCollection,
            ResourceCollection = resourceCollection,
            TaskStore = new InMemoryMcpTaskStore()
        };
        options.ConfigureDynamicCatalog();
        return options;
    }
}
