using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace DevTools.McpServer;

internal static class ToolHelpers
{
    public static CallToolResult ErrorResult(string message) =>
        new() { IsError = true, Content = [new TextContentBlock { Text = message }] };

    public static RevitBridgeClient? ResolveClient(
        InstanceManager instanceManager,
        IDictionary<string, JsonElement> args,
        out Dictionary<string, JsonElement> cleanedArgs)
    {
        cleanedArgs = new Dictionary<string, JsonElement>(args);
        if (cleanedArgs.Remove("revitInstanceId", out var pidElement))
            return instanceManager.GetByProcessId(InstanceManager.ParseProcessId(pidElement));
        return instanceManager.GetDefault();
    }

    public static string FormatInstanceListing(InstanceManager instanceManager)
    {
        var instances = instanceManager.GetInstances();
        if (instances.Count == 0)
            return "No Revit instances connected.";
        return "Multiple Revit instances available. Specify 'revitInstanceId': " +
               string.Join(", ", instances.Select(i =>
                   $"PID {i.ProcessId} ({i.DocumentTitle ?? "no document"})"));
    }
}
