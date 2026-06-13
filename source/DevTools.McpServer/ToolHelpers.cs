using System.Text.Json;
using DevTools.McpParser.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.McpServer;

internal static class ToolHelpers
{
    public static JsonSerializerOptions IndentedJsonOptions { get; } = new()
    {
        WriteIndented = true
    };
    
    public static CallToolResult ErrorResult(string message) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = message }] };

    public static HostBridgeClient? ResolveClient(
        InstanceManager instanceManager,
        IDictionary<string, JsonElement> args,
        out Dictionary<string, JsonElement> cleanedArgs)
    {
        cleanedArgs = new Dictionary<string, JsonElement>(args);
        if (cleanedArgs.Remove(McpPropertyNames.HostInstanceId, out var pidElement))
            return instanceManager.GetByProcessId(InstanceManager.ParseProcessId(pidElement));
        return instanceManager.GetDefault();
    }

    public static string FormatInstanceListing(InstanceManager instanceManager)
    {
        var instances = instanceManager.GetInstances();
        if (instances.Count == 0)
            return "No host instances connected.";
        return $"Multiple host instances available. Specify '{McpPropertyNames.HostInstanceId}': " +
               string.Join(", ", instances.Select(i =>
                   $"PID {i.ProcessId} ({i.HostApp ?? "unknown"} {i.VersionNumber})"));
    }
    
    public static void ConfigureDynamicCatalog(this McpServerOptions options)
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
            ResourceCollection = resourceCollection
        };
        options.ConfigureDynamicCatalog();
        return options;
    }
}
