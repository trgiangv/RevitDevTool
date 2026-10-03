using DevTools.Execution.Abstractions;

namespace DevTools.Mcp.Core.Models;

public sealed record PrimitiveBinding
{
    public ExecutionMode SourceKind { get; private init; } = ExecutionMode.Python;
    public string ContainerType { get; private init; } = string.Empty;
    public string MethodName { get; private init; } = string.Empty;
    public string SourcePath { get; private init; } = string.Empty;
    public string SourceAddress { get; private init; } = string.Empty;
    public string GroupName { get; private init; } = string.Empty;

    public static PrimitiveBinding Create(
        ExecutionMode sourceKind,
        string? sourcePath,
        string? containerType,
        string? methodName,
        string sourceAddress,
        string groupName) =>
        new()
        {
            SourceKind = sourceKind,
            ContainerType = containerType?.Trim() ?? string.Empty,
            MethodName = methodName?.Trim() ?? string.Empty,
            SourcePath = sourcePath?.Trim() ?? string.Empty,
            SourceAddress = sourceAddress.Trim(),
            GroupName = groupName.Trim(),
        };

    public static string CreatePrimitiveId(string? name, string? sourceAddress) => $"{name}_[{sourceAddress}]";
}
