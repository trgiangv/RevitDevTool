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

    public static IHostBridgeClient? ResolveClient(
        IInstanceManager instanceManager,
        IDictionary<string, JsonElement> args,
        out Dictionary<string, JsonElement> cleanedArgs)
    {
        cleanedArgs = new Dictionary<string, JsonElement>(args);
        if (cleanedArgs.Remove(McpPropertyNames.HostInstanceId, out var pidElement))
            return instanceManager.GetByProcessId(ParseProcessId(pidElement));
        return instanceManager.GetDefault();
    }

    /// <summary>
    /// Resolves a host bridge client from the request _meta bag (e.g. resource reads
    /// that don't have tool arguments but may carry host instance hints).
    /// </summary>
    public static IHostBridgeClient? ResolveClientFromMeta(
        IReadOnlyDictionary<string, JsonElement>? meta,
        IInstanceManager instanceManager)
    {
        if (meta is null) return null;
        if (!meta.TryGetValue(McpPropertyNames.HostInstanceId, out var pidElement)) return null;
        var pid = ParseProcessId(pidElement);
        return pid > 0 ? instanceManager.GetByProcessId(pid) : null;
    }

    public static string FormatInstanceListing(IInstanceManager instanceManager)
    {
        var instances = instanceManager.GetInstances();
        if (instances.Count == 0)
            return "No host instances connected.";
        return $"Multiple host instances available. Specify '{McpPropertyNames.HostInstanceId}': " +
               string.Join(", ", instances.Select(i =>
                   $"PID {i.ProcessId} ({i.HostApp ?? "unknown"} {i.VersionNumber})"));
    }

    public static int ParseProcessId(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
            return element.GetInt32();

        return int.TryParse(element.GetString(), out var pid) ? pid : 0;
    }

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
