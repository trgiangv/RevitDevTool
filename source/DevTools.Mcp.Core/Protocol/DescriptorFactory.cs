using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using IconKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Icon;

namespace DevTools.Mcp.Core.Protocol;

/// <summary>Builds host descriptors from SDK or parser metadata.</summary>
public static class DescriptorFactory
{
    public static McpToolDescriptor FromTool(Tool tool) =>
        new()
        {
            Name = tool.Name,
            Title = tool.Title,
            Description = tool.Description,
            InputSchema = tool.InputSchema,
            OutputSchema = tool.OutputSchema,
            Annotations = ToolAnnotations(tool.Annotations),
            Meta = CloneMeta(tool.Meta),
            Icons = SerializeIcons(tool.Icons),
        };

    public static McpToolAnnotations? ToolHints(
        string? title,
        bool? readOnly = null,
        bool? destructive = null,
        bool? idempotent = null,
        bool? openWorld = null,
        string? iconSource = null)
    {
        if (string.IsNullOrWhiteSpace(title)
            && readOnly is null
            && destructive is null
            && idempotent is null
            && openWorld is null
            && string.IsNullOrWhiteSpace(iconSource))
        {
            return null;
        }

        return new McpToolAnnotations
        {
            Destructive = destructive,
            Idempotent = idempotent,
            OpenWorld = openWorld,
            ReadOnly = readOnly,
            Title = title,
            IconSource = iconSource,
        };
    }

    public static McpResourceDescriptor FromResource(Resource resource) =>
        new()
        {
            Uri = resource.Uri,
            Name = resource.Name,
            Title = resource.Title,
            Description = resource.Description,
            MimeType = resource.MimeType,
            Size = resource.Size,
            Meta = CloneMeta(resource.Meta),
            Icons = SerializeIcons(resource.Icons),
            Annotations = ResourceAnnotations(resource.Annotations),
        };

    public static McpResourceTemplateDescriptor FromTemplate(ResourceTemplate template) =>
        new()
        {
            UriTemplate = template.UriTemplate,
            Name = template.Name,
            Title = template.Title,
            Description = template.Description,
            MimeType = template.MimeType,
            Meta = CloneMeta(template.Meta),
            Icons = SerializeIcons(template.Icons),
            Annotations = ResourceAnnotations(template.Annotations),
        };

    public static IList<Icon>? ToSdkIcons(JsonArray? icons)
    {
        if (icons is null || icons.Count == 0)
            return null;

        var list = new List<Icon>();
        foreach (var node in icons)
        {
            if (node is not JsonObject obj)
                continue;

            var source = obj[IconKeys.Src]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(source))
                continue;

            list.Add(new Icon { Source = source! });
        }

        return list.Count == 0 ? null : list;
    }

    private static McpToolAnnotations? ToolAnnotations(ToolAnnotations? annotations)
    {
        if (annotations is null)
            return null;

        return new McpToolAnnotations
        {
            Destructive = annotations.DestructiveHint,
            Idempotent = annotations.IdempotentHint,
            OpenWorld = annotations.OpenWorldHint,
            ReadOnly = annotations.ReadOnlyHint,
            Title = annotations.Title,
        };
    }

    private static McpResourceAnnotations? ResourceAnnotations(Annotations? annotations)
    {
        if (annotations is null)
            return null;

        return new McpResourceAnnotations
        {
            Priority = annotations.Priority,
        };
    }

    private static JsonObject? CloneMeta(JsonObject? meta) => meta?.DeepClone().AsObject();

    private static JsonArray? SerializeIcons(IList<Icon>? icons)
    {
        if (icons is null || icons.Count == 0)
            return null;

        var array = new JsonArray();
        foreach (var icon in icons)
        {
            array.Add(new JsonObject
            {
                [IconKeys.Src] = icon.Source,
            });
        }

        return array;
    }
}
