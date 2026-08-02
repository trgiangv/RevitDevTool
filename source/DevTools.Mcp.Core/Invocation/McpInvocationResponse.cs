using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core;

/// <summary>Lossless application representation of an SDK content block.</summary>
public abstract record McpContent
{
    public Annotations? Annotations { get; init; }
    public JsonObject? Meta { get; init; }
}

public sealed record McpTextContent(string Text) : McpContent;
public sealed record McpImageContent(byte[] Data, string MimeType) : McpContent;
public sealed record McpAudioContent(byte[] Data, string MimeType) : McpContent;

public abstract record McpEmbeddedResourceContent(string Uri, string? MimeType) : McpContent
{
    public JsonObject? ResourceMeta { get; init; }
}

public sealed record McpEmbeddedTextResourceContent(string Uri, string Text, string? MimeType)
    : McpEmbeddedResourceContent(Uri, MimeType);

public sealed record McpEmbeddedBlobResourceContent(string Uri, byte[] Blob, string? MimeType)
    : McpEmbeddedResourceContent(Uri, MimeType);

public sealed record McpResourceLinkContent(
    string Uri,
    string Name,
    string? Title = null,
    string? Description = null,
    string? MimeType = null,
    long? Size = null) : McpContent;

public sealed record McpInvocationResponse
{
    public IReadOnlyList<McpContent> Content { get; init; } = [];
    public bool? IsError { get; init; }
    public JsonElement? StructuredContent { get; init; }
    public JsonObject? Meta { get; init; }
}
