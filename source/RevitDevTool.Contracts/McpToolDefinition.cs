using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;

namespace RevitDevTool.Contracts;

public sealed class McpToolDefinition
{
    public string ToolId { get; set; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string InputSchemaJson { get; init; } = "{}";
    [JsonPropertyName("annotations")]
    public ToolAnnotations? Annotations { get; init; }
    public ExecutionMode SourceKind { get; set; } = ExecutionMode.Python;
    public string ContainerType { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string SourceAddress { get; set; } = string.Empty;
    public string GroupKey { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;

    public void EnsureIdentity()
    {
        if (string.IsNullOrWhiteSpace(SourceAddress))
            SourceAddress = BuildFallbackSourceAddress(SourceKind, SourcePath, ContainerType, MethodName);

        if (string.IsNullOrWhiteSpace(GroupKey))
            GroupKey = SourcePath.Trim();

        if (string.IsNullOrWhiteSpace(GroupName))
            GroupName = BuildFallbackGroupName(SourceKind, SourcePath, ContainerType);

        if (string.IsNullOrWhiteSpace(DisplayName))
            DisplayName = Name;

        if (!string.IsNullOrWhiteSpace(ToolId))
            return;

        ToolId = CreateToolId(Name, SourceAddress);
    }

    public static string CreateToolId(string? name, string? sourceAddress)
    {
        return $"{NormalizeIdentitySegment(name)}_[{NormalizeIdentitySegment(sourceAddress)}]";
    }

    private static string BuildFallbackSourceAddress(
        ExecutionMode sourceKind,
        string? sourcePath,
        string? containerType,
        string? methodName)
    {
        var location = sourceKind switch
        {
            ExecutionMode.Assembly => Path.GetFileName(sourcePath) ?? string.Empty,
            _ => Path.GetFileNameWithoutExtension(sourcePath) ?? string.Empty
        };
        var container = containerType?.Trim() ?? string.Empty;
        var method = methodName?.Trim() ?? string.Empty;

        return string.Join(":", new[]
        {
            location,
            string.Join(".", new[] { container, method }.Where(value => !string.IsNullOrWhiteSpace(value)))
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string BuildFallbackGroupName(ExecutionMode sourceKind, string? sourcePath, string? containerType)
    {
        var sourceName = sourceKind == ExecutionMode.Assembly
            ? Path.GetFileName(sourcePath)
            : Path.GetFileName(Path.GetDirectoryName(sourcePath ?? string.Empty) ?? string.Empty);

        return !string.IsNullOrWhiteSpace(sourceName)
            ? sourceName
            : containerType?.Trim() ?? "MCP Toolset";
    }

    private static string NormalizeIdentitySegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        return value!.Trim()
            .Replace('\\', '/')
            .Replace(' ', '-')
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);
    }
}
