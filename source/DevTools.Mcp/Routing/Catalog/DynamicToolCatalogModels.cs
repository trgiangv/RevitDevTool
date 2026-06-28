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
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("registrations")] IReadOnlyList<DynamicToolRegistration> Registrations);

[UsedImplicitly]
public sealed record DynamicToolRegistration(
    [property: JsonPropertyName("hostInstanceId")] int HostInstanceId,
    [property: JsonPropertyName("hostApp")] string? HostApp,
    [property: JsonPropertyName("versionNumber")] string? VersionNumber,
    [property: JsonPropertyName("pipeName")] string PipeName,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("inputSchema")] JsonElement InputSchema);
