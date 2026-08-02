using System.Text.Json.Nodes;
using ResourceKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Resources;
using ToolsKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Tools;
using IconKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Icon;

namespace DevTools.Mcp.Core.Protocol;

/// <summary>Encodes catalog descriptors to MCP list-result wire shape without SDK types.</summary>
public static class CatalogListEncoder
{
    public static JsonNode Tools(IReadOnlyList<McpToolDescriptor> tools)
    {
        var array = new JsonArray();
        foreach (var tool in tools)
            array.Add(Tool(tool));
        return new JsonObject { [ToolsKeys.List] = array };
    }

    public static JsonNode Resources(IReadOnlyList<McpResourceDescriptor> resources)
    {
        var array = new JsonArray();
        foreach (var resource in resources)
            array.Add(Resource(resource));
        return new JsonObject { [ResourceKeys.List] = array };
    }

    public static JsonNode ResourceTemplates(IReadOnlyList<McpResourceTemplateDescriptor> templates)
    {
        var array = new JsonArray();
        foreach (var template in templates)
            array.Add(ResourceTemplate(template));
        return new JsonObject { [ResourceKeys.ResourceTemplates] = array };
    }

    public static JsonObject Tool(McpToolDescriptor tool)
    {
        var json = new JsonObject
        {
            [ToolsKeys.Name] = tool.Name,
        };

        if (!string.IsNullOrWhiteSpace(tool.Title))
            json[ToolsKeys.Title] = tool.Title;
        if (!string.IsNullOrWhiteSpace(tool.Description))
            json[ToolsKeys.Description] = tool.Description;
        if (tool.InputSchema is { } inputSchema)
            json[ToolsKeys.InputSchema] = JsonNode.Parse(inputSchema.GetRawText());
        if (tool.OutputSchema is { } outputSchema)
            json[ToolsKeys.OutputSchema] = JsonNode.Parse(outputSchema.GetRawText());
        if (tool.Annotations is { } annotations)
            json[ToolsKeys.Annotations] = WriteToolAnnotations(annotations);
        if (tool.Meta is not null)
            json[ToolsKeys.Meta] = tool.Meta.DeepClone();
        if (tool.Icons is not null)
            json[IconKeys.List] = tool.Icons.DeepClone();

        return json;
    }

    public static JsonObject Resource(McpResourceDescriptor resource)
    {
        var json = new JsonObject
        {
            [ResourceKeys.Uri] = resource.Uri,
            [ToolsKeys.Name] = resource.Name,
        };

        if (!string.IsNullOrWhiteSpace(resource.Title))
            json[ToolsKeys.Title] = resource.Title;
        if (!string.IsNullOrWhiteSpace(resource.Description))
            json[ToolsKeys.Description] = resource.Description;
        if (!string.IsNullOrWhiteSpace(resource.MimeType))
            json[ResourceKeys.MimeType] = resource.MimeType;
        if (resource.Size is { } size)
            json[ResourceKeys.Size] = size;
        if (resource.Annotations is { } annotations)
            json[ToolsKeys.Annotations] = WriteResourceAnnotations(annotations);
        if (resource.Meta is not null)
            json[ToolsKeys.Meta] = resource.Meta.DeepClone();
        if (resource.Icons is not null)
            json[IconKeys.List] = resource.Icons.DeepClone();

        return json;
    }

    public static JsonObject ResourceTemplate(McpResourceTemplateDescriptor template)
    {
        var json = new JsonObject
        {
            [ResourceKeys.UriTemplate] = template.UriTemplate,
            [ToolsKeys.Name] = template.Name,
        };

        if (!string.IsNullOrWhiteSpace(template.Title))
            json[ToolsKeys.Title] = template.Title;
        if (!string.IsNullOrWhiteSpace(template.Description))
            json[ToolsKeys.Description] = template.Description;
        if (!string.IsNullOrWhiteSpace(template.MimeType))
            json[ResourceKeys.MimeType] = template.MimeType;
        if (template.Annotations is { } annotations)
            json[ToolsKeys.Annotations] = WriteResourceAnnotations(annotations);
        if (template.Meta is not null)
            json[ToolsKeys.Meta] = template.Meta.DeepClone();
        if (template.Icons is not null)
            json[IconKeys.List] = template.Icons.DeepClone();

        return json;
    }

    private static JsonObject WriteToolAnnotations(McpToolAnnotations annotations)
    {
        var json = new JsonObject();
        if (annotations.Destructive is { } destructive)
            json["destructiveHint"] = destructive;
        if (annotations.Idempotent is { } idempotent)
            json["idempotentHint"] = idempotent;
        if (annotations.OpenWorld is { } openWorld)
            json["openWorldHint"] = openWorld;
        if (annotations.ReadOnly is { } readOnly)
            json["readOnlyHint"] = readOnly;
        if (!string.IsNullOrWhiteSpace(annotations.Title))
            json["title"] = annotations.Title;
        if (!string.IsNullOrWhiteSpace(annotations.IconSource))
            json["iconSource"] = annotations.IconSource;
        return json;
    }

    private static JsonObject WriteResourceAnnotations(McpResourceAnnotations annotations)
    {
        var json = new JsonObject();
        if (annotations.Priority is { } priority)
            json["priority"] = priority;
        return json;
    }
}
