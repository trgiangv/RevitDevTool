using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Routing.Catalog;

public sealed record DynamicToolCatalogEntry(Tool Tool, InstanceInfo Instance, string PipeName);

public enum DynamicToolResolutionState
{
    Found,
    NotFound,
    Ambiguous
}

public sealed record DynamicToolResolution(
    DynamicToolResolutionState State,
    DynamicToolCatalogEntry? Registration,
    IReadOnlyList<DynamicToolCatalogEntry> Candidates);

[UsedImplicitly]
public sealed record DynamicCatalogSummary(
    [property: JsonPropertyName("tools")] IReadOnlyList<DynamicToolSummary> Tools,
    [property: JsonPropertyName("toolCount")] int ToolCount,
    [property: JsonPropertyName("registrationCount")] int RegistrationCount);

[UsedImplicitly]
public sealed record DynamicToolSummary(
    [property: JsonPropertyName(IpcPropertyNames.Name)] string Name,
    [property: JsonPropertyName("registrations")] IReadOnlyList<DynamicToolRegistration> Registrations);

[UsedImplicitly]
public sealed record DynamicToolRegistration(
    [property: JsonPropertyName(McpPropertyNames.HostInstanceId)] int HostInstanceId,
    [property: JsonPropertyName(IpcPropertyNames.HostApp)] string? HostApp,
    [property: JsonPropertyName(IpcPropertyNames.VersionNumber)] string? VersionNumber,
    [property: JsonPropertyName(IpcPropertyNames.PipeName)] string PipeName,
    [property: JsonPropertyName(McpPropertyNames.Description)] string? Description,
    [property: JsonPropertyName("inputSchema")] JsonElement InputSchema);
