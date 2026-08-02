using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace McpToolsetDemo;

[McpServerResourceType]
public static class McpSampleResources
{
    [McpServerResource(
        UriTemplate = "sample://demo/status",
        Name = "demo_status",
        Title = "Demo Status Resource",
        MimeType = "text/plain",
        IconSource = "https://example.com/icons/resource-status.png")]
    [McpMeta("resourceKind", "status")]
    [Description("Returns a static parser validation resource.")]
    public static string DemoStatus()
    {
        return string.Concat("o", "k");
    }

    [McpServerResource(
        UriTemplate = "sample://demo/views/{viewId}",
        Name = "demo_view",
        Title = "Demo View Resource",
        MimeType = "application/json",
        IconSource = "https://example.com/icons/resource-view.png")]
    [McpMeta("resourceKind", "view")]
    [Description("Returns a template resource for a demo view.")]
    public static string DemoView([Description("Identifier for the target view.")] string viewId)
    {
        return $"{{\"viewId\":\"{viewId}\"}}";
    }

    [McpServerResource(Name = "demo_level")]
    [Description("Returns a derived resource URI template for a level.")]
    public static string DemoLevel(
        [Description("Identifier for the target level.")] string levelId,
        CancellationToken cancellationToken = default,
        IServiceProvider? serviceProvider = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = serviceProvider;
        return JsonSerializer.Serialize(new { levelId });
    }

    [McpServerResource(Name = "demo_health", MimeType = "text/plain")]
    [Description("Health check resource without explicit UriTemplate.")]
    public static string DemoHealth()
    {
        return "healthy";
    }
}
