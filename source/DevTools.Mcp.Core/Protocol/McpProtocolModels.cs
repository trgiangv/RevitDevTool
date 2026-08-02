using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DevTools.Mcp.Core.Protocol;

/// <summary>
/// Host-owned tool descriptor (MCP <c>tools/list</c> item shape). Replaces SDK <c>Tool</c> on host.
/// </summary>
public sealed record McpToolDescriptor
{
    public required string Name { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public JsonElement? InputSchema { get; init; }

    public JsonElement? OutputSchema { get; init; }

    public McpToolAnnotations? Annotations { get; init; }

    public JsonObject? Meta { get; init; }

    public JsonArray? Icons { get; init; }
}

/// <summary>MCP tool annotation flags (spec subset).</summary>
public sealed record McpToolAnnotations
{
    public bool? Destructive { get; init; }

    public bool? Idempotent { get; init; }

    public bool? OpenWorld { get; init; }

    public bool? ReadOnly { get; init; }

    public string? Title { get; init; }

    public string? IconSource { get; init; }
}

/// <summary>MCP resource annotation fields (spec subset).</summary>
public sealed record McpResourceAnnotations
{
    public double? Priority { get; init; }
}

/// <summary>
/// Host-owned resource descriptor (MCP <c>resources/list</c> item shape).
/// </summary>
public sealed record McpResourceDescriptor
{
    public required string Uri { get; init; }

    public required string Name { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? MimeType { get; init; }

    public long? Size { get; init; }

    public JsonObject? Meta { get; init; }

    public JsonArray? Icons { get; init; }

    public McpResourceAnnotations? Annotations { get; init; }
}

/// <summary>
/// Host-owned resource template descriptor (MCP <c>resources/templates/list</c>).
/// </summary>
public sealed record McpResourceTemplateDescriptor
{
    public required string UriTemplate { get; init; }

    public required string Name { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? MimeType { get; init; }

    public JsonObject? Meta { get; init; }

    public JsonArray? Icons { get; init; }

    public McpResourceAnnotations? Annotations { get; init; }
}

[JsonSerializable(typeof(McpToolDescriptor))]
[JsonSerializable(typeof(McpToolAnnotations))]
[JsonSerializable(typeof(McpResourceAnnotations))]
[JsonSerializable(typeof(McpResourceDescriptor))]
[JsonSerializable(typeof(McpResourceTemplateDescriptor))]
[JsonSerializable(typeof(McpInvocationRequest))]
[JsonSerializable(typeof(McpReadResourceResponse))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
public partial class McpProtocolJsonContext : JsonSerializerContext;

/// <summary>Host-owned <c>resources/read</c> result (spec <c>contents</c> array).</summary>
public sealed record McpReadResourceResponse
{
    public IReadOnlyList<McpReadResourceContent> Contents { get; init; } = [];
}

public abstract record McpReadResourceContent(string Uri, string? MimeType);

public sealed record McpReadResourceTextContent(string Uri, string Text, string? MimeType)
    : McpReadResourceContent(Uri, MimeType);

public sealed record McpReadResourceBlobContent(string Uri, byte[] Blob, string? MimeType)
    : McpReadResourceContent(Uri, MimeType);
