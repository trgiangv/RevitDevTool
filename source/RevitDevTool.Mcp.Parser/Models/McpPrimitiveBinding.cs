using RevitDevTool.Contracts;
// ReSharper disable RedundantSuppressNullableWarningExpression
namespace RevitDevTool.Mcp.Parser.Models;

public sealed record McpPrimitiveBinding
{
    public ExecutionMode SourceKind { get; private init; } = ExecutionMode.Python;
    public string ContainerType { get; private init; } = string.Empty;
    public string MethodName { get; private init; } = string.Empty;
    public string SourcePath { get; private init; } = string.Empty;
    public string SourceAddress { get; private init; } = string.Empty;
    public string GroupName { get; private init; } = string.Empty;

    public static McpPrimitiveBinding Create(
        ExecutionMode sourceKind,
        string? sourcePath,
        string? containerType,
        string? methodName,
        string? sourceAddress = null,
        string? groupName = null)
    {
        var normalizedSourcePath = sourcePath?.Trim() ?? string.Empty;
        var normalizedContainerType = containerType?.Trim() ?? string.Empty;
        var normalizedMethodName = methodName?.Trim() ?? string.Empty;
        var normalizedSourceAddress = string.IsNullOrWhiteSpace(sourceAddress)
            ? BuildFallbackSourceAddress(sourceKind, normalizedSourcePath, normalizedContainerType, normalizedMethodName)
            : sourceAddress!.Trim();

        return new McpPrimitiveBinding
        {
            SourceKind = sourceKind,
            ContainerType = normalizedContainerType,
            MethodName = normalizedMethodName,
            SourcePath = normalizedSourcePath,
            SourceAddress = normalizedSourceAddress,
            GroupName = string.IsNullOrWhiteSpace(groupName)
                ? BuildFallbackGroupName(sourceKind, normalizedSourcePath, normalizedContainerType)
                : groupName!.Trim()
        };
    }

    private static string BuildFallbackSourceAddress(
        ExecutionMode sourceKind,
        string sourcePath,
        string containerType,
        string methodName)
    {
        var location = sourceKind switch
        {
            ExecutionMode.Assembly => Path.GetFileName(sourcePath),
            _ => Path.GetFileNameWithoutExtension(sourcePath)
        };

        return string.Join(":", new[]
        {
            location,
            string.Join(".", new[] { containerType, methodName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string BuildFallbackGroupName(ExecutionMode sourceKind, string sourcePath, string containerType)
    {
        var sourceName = sourceKind == ExecutionMode.Assembly
            ? Path.GetFileName(sourcePath)
            : Path.GetFileName(Path.GetDirectoryName(sourcePath) ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(sourceName))
            return sourceName;

        return string.IsNullOrWhiteSpace(containerType) ? "MCP Toolset" : containerType;
    }

    public static string CreatePrimitiveId(string? name, string? sourceAddress)
    {
        return $"{NormalizeSegment(name)}_[{NormalizeSegment(sourceAddress)}]";
    }

    private static string NormalizeSegment(string? value)
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
