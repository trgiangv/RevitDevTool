using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp;

/// <summary>Stable SHA-256 fingerprint of catalog item metadata (ids alone are insufficient for list_changed).</summary>
public static class McpCatalogContentHash
{
    public static string Compute(RegistryCatalog catalog)
    {
        var builder = new StringBuilder();
        foreach (var tool in catalog.Tools.OrderBy(t => t.Id, StringComparer.Ordinal))
            AppendTool(builder, tool);
        foreach (var resource in catalog.Resources.OrderBy(r => r.Id, StringComparer.Ordinal))
            AppendResource(builder, resource);

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static void AppendTool(StringBuilder builder, RegisteredTool tool)
    {
        var d = tool.Descriptor;
        builder.Append("tool|").Append(tool.Id).Append('|');
        builder.Append(d.Name).Append('|');
        builder.Append(d.Description).Append('|');
        builder.Append(SerializeSchema(d.InputSchema)).Append('|');
        builder.Append(SerializeSchema(d.OutputSchema)).Append('|');
        builder.Append(SerializeJson(d.Annotations)).Append('\n');
    }

    private static void AppendResource(StringBuilder builder, RegisteredResource resource)
    {
        builder.Append("resource|").Append(resource.Id).Append('|');
        if (resource.Descriptor is { } fixedResource)
        {
            builder.Append(fixedResource.Name).Append('|');
            builder.Append(fixedResource.Description).Append('|');
            builder.Append(fixedResource.Uri).Append('|');
            builder.Append(fixedResource.MimeType).Append('|');
            builder.Append(SerializeJson(fixedResource.Annotations)).Append('\n');
            return;
        }

        if (resource.TemplateDescriptor is { } template)
        {
            builder.Append(template.Name).Append('|');
            builder.Append(template.Description).Append('|');
            builder.Append(template.UriTemplate).Append('|');
            builder.Append(template.MimeType).Append('|');
            builder.Append(SerializeJson(template.Annotations)).Append('\n');
        }
    }

    private static string SerializeSchema(JsonElement? schema) =>
        schema is { } element ? element.GetRawText() : string.Empty;

    private static string SerializeJson<T>(T? value) =>
        value is null ? string.Empty : JsonSerializer.Serialize(value, ToolHelpers.ProtocolOptions);
}
