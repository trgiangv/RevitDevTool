using ModelContextProtocol.Server;

namespace DevTools.Mcp.Catalog.Discovery;

/// <summary>
/// Builds SDK <see cref="McpServerResourceCreateOptions"/> / <see cref="McpServerToolCreateOptions"/>
/// from parsed catalog metadata so isolated toolset invoke uses advertised URIs and tool names,
/// not SDK-inferred <c>resource://mcp/*</c> fallbacks.
/// </summary>
public static class McpCatalogCreateOptions
{
    public static McpServerResourceCreateOptions ForResource(McpRegisteredResource resource, IServiceProvider? services = null)
    {
        var template = resource.TemplateDescriptor;
        var fixedResource = resource.Descriptor;
        return new McpServerResourceCreateOptions
        {
            Services = services,
            UriTemplate = fixedResource?.Uri ?? template?.UriTemplate,
            Name = fixedResource?.Name ?? template?.Name,
            Title = fixedResource?.Title ?? template?.Title,
            Description = fixedResource?.Description ?? template?.Description,
            MimeType = fixedResource?.MimeType ?? template?.MimeType,
            Meta = fixedResource?.Meta ?? template?.Meta,
            Icons = DescriptorFactory.ToSdkIcons(fixedResource?.Icons ?? template?.Icons),
        };
    }

    public static McpServerToolCreateOptions ForTool(McpRegisteredTool tool, IServiceProvider? services = null)
    {
        var descriptor = tool.Descriptor;
        return new McpServerToolCreateOptions
        {
            Services = services,
            Name = descriptor.Name,
            Title = descriptor.Title,
            Description = descriptor.Description,
            UseStructuredContent = descriptor.OutputSchema is not null,
            Meta = descriptor.Meta,
        };
    }
}
