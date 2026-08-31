using ModelContextProtocol.Protocol;
namespace DevTools.Mcp.Core.Models;

public sealed record McpRegisteredResource
{
    public required string Id { get; init; }

    public Resource? Descriptor { get; init; }

    public ResourceTemplate? TemplateDescriptor { get; init; }

    public required McpPrimitiveBinding Binding { get; init; }

    public string DisplayName => Descriptor?.Name ?? TemplateDescriptor?.Name ?? string.Empty;
}
