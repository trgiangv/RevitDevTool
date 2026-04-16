using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace McpToolsetDemo;

public enum DemoCategory
{
    General,
    Technical,
    Business,
}

[McpServerToolType]
public static class McpSampleTools
{
    [McpServerTool(
        Name = "get_demo_status",
        Title = "Get Demo Status",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Return demo status for MCP parser validation.")]
    public static object GetDemoStatus()
    {
        return new
        {
            status = "success",
            summary = "Demo MCP tool is reachable.",
            data = new
            {
                language = "csharp",
                sample = true,
            },
            warnings = Array.Empty<string>(),
        };
    }

    [McpServerTool(
        Name = "get_advanced_demo_status",
        Title = "Get Advanced Demo Status",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false,
        IconSource = "https://dohoasaigon.com/wp-content/uploads/2025/03/revit-2024.png")]
    [McpMeta("version", "1.0")]
    [McpMeta("isBeta", true)]
    [Description("Return advanced demo status for MCP parser validation.")]
    public static object GetAdvancedDemoStatus(
        [Description("Topic to inspect.")] string topic,
        CancellationToken cancellationToken,
        IServiceProvider serviceProvider,
        McpServer server,
        IProgress<ProgressNotificationValue> progress,
        [FromKeyedServices("demo")] object dependency)
    {
        progress.Report(new ProgressNotificationValue { Progress = 1, Message = $"Inspecting {topic}" });
        cancellationToken.ThrowIfCancellationRequested();
        _ = serviceProvider;
        _ = server;
        _ = dependency;
        return new
        {
            topic,
            status = "advanced-success",
        };
    }

    [McpServerTool(Name = "get_nullable_count")]
    [Description("Returns a count with nullable parameter for parser validation.")]
    public static object GetNullableCount(
        [Description("Item name.")] string name,
        [Description("Optional count.")] int? count = null)
    {
        return new { name, count = count ?? 0 };
    }

    [McpServerTool(Name = "ping_infrastructure")]
    [Description("Tool with only infrastructure parameters for empty-schema validation.")]
    public static string PingInfrastructure(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return "pong";
    }

    [McpServerTool(Name = "categorize_item")]
    [Description("Categorizes an item using an enum parameter.")]
    public static object CategorizeItem(
        [Description("Item name.")] string item,
        [Description("Category.")] DemoCategory category)
    {
        return new { item, category = category.ToString() };
    }

    [McpServerTool(
        Name = "get_nested_meta",
        Title = "Get Nested Meta",
        Destructive = true,
        OpenWorld = true)]
    [McpMeta("version", "2.0")]
    [McpMeta("flags", JsonValue = "{\"nested\":1,\"active\":true}")]
    [Description("Tool with nested JSON meta and destructive/openWorld annotations.")]
    public static object GetNestedMeta([Description("Input key.")] string key)
    {
        return new { key, result = "nested-meta-ok" };
    }
}