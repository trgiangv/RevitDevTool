using DevTools.Mcp.Core.Protocol;

namespace DevTools.Mcp.Core;

public sealed record McpRegisteredResource
{
    public required string Id { get; init; }

    public McpResourceDescriptor? Descriptor { get; init; }

    public McpResourceTemplateDescriptor? TemplateDescriptor { get; init; }

    public required McpPrimitiveBinding Binding { get; init; }

    public string DisplayName => Descriptor?.Name ?? TemplateDescriptor?.Name ?? string.Empty;
}
