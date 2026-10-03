using ModelContextProtocol.Protocol;
namespace DevTools.Mcp.Core.Models;

public sealed record RegisteredResource
{
    public required string Id { get; init; }

    public Resource? Descriptor { get; init; }

    public ResourceTemplate? TemplateDescriptor { get; init; }

    public required PrimitiveBinding Binding { get; init; }

    public string DisplayName => Descriptor?.Name ?? TemplateDescriptor?.Name ?? string.Empty;
}
