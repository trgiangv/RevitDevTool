using System.IO;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Server;
using RevitDevTool.Contracts;
using RevitDevTool.Execution.Providers.Dotnet;
namespace RevitDevTool.Mcp.Dotnet;

public static class DotnetMcpAssemblyParser
{
    private static readonly string McpToolTypeAttributeName = typeof(McpServerToolTypeAttribute).FullName!;
    private static readonly string McpToolAttributeName = typeof(McpServerToolAttribute).FullName!;
    private static readonly string DescriptionAttributeTypeName = typeof(System.ComponentModel.DescriptionAttribute).FullName!;

    private sealed class ToolMetadata
    {
        public string Name { get; init; } = string.Empty;
        public string? RawDescription { get; init; }
    }

    public static IReadOnlyList<McpToolDefinition> ParseToolsFromAssembly(string assemblyPath)
    {
        var tools = new List<McpToolDefinition>();
        var resolutionPaths = AssemblyLoaderService.CollectAssemblyPaths(assemblyPath);
        var resolver = new PathAssemblyResolver(resolutionPaths);
        using var metadataLoadContext = new MetadataLoadContext(resolver);
        var assembly = metadataLoadContext.LoadFromAssemblyPath(assemblyPath);

        foreach (var type in GetCandidateToolTypes(assembly))
        {
            foreach (var method in GetCandidateToolMethods(type))
            {
                var definition = TryBuildToolDefinition(type, method, assemblyPath);
                if (definition is not null)
                    tools.Add(definition);
            }
        }

        return tools;
    }

    private static IEnumerable<Type> GetCandidateToolTypes(Assembly assembly)
    {
        return GetMetadataTypes(assembly)
            .OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase)
            .Where(type => HasAttribute(type.CustomAttributes, McpToolTypeAttributeName));
    }

    private static IEnumerable<MethodInfo> GetCandidateToolMethods(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static McpToolDefinition? TryBuildToolDefinition(Type type, MethodInfo method, string assemblyPath)
    {
        try
        {
            var toolAttribute = FindToolAttribute(method);
            if (toolAttribute is null)
                return null;

            var metadata = ReadToolMetadata(toolAttribute, method);
            var description = !string.IsNullOrWhiteSpace(metadata.RawDescription)
                ? metadata.RawDescription!.Trim()
                : $"MCP tool from {type.FullName}";
            var schema = DotnetMcpSchemaAdapter.BuildInputSchema(method);
            // ReSharper disable once ConstantNullCoalescingCondition
            var assemblyName = Path.GetFileName(assemblyPath) ?? assemblyPath;
            var sourceAddress = $"{assemblyName}:{type.FullName}.{method.Name}";
            return new McpToolDefinition
            {
                ToolId = McpToolDefinition.CreateToolId(metadata.Name, sourceAddress),
                Name = metadata.Name,
                Description = description,
                InputSchemaJson = JsonSerializer.Serialize(schema),
                SourceKind = ExecutionMode.Assembly,
                ContainerType = type.FullName ?? string.Empty,
                MethodName = method.Name,
                SourcePath = assemblyPath,
                SourceAddress = sourceAddress,
                GroupKey = Path.GetFullPath(assemblyPath),
                GroupName = assemblyName
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning(
                $"[MCP] Skip .NET tool '{type.FullName}.{method.Name}' in '{assemblyPath}': {ex.Message}");
            return null;
        }
    }

    private static CustomAttributeData? FindToolAttribute(MethodInfo method)
    {
        return method.CustomAttributes.FirstOrDefault(attr => attr.AttributeType.FullName == McpToolAttributeName);
    }

    private static string? ExtractNamedString(CustomAttributeData attr, string memberName)
    {
        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg.MemberName == memberName && namedArg.TypedValue.Value is string value && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static ToolMetadata ReadToolMetadata(CustomAttributeData toolAttribute, MethodInfo method)
    {
        var name = ExtractNamedString(toolAttribute, "Name") ?? method.Name;
        var rawDescription = ExtractNamedString(toolAttribute, "Description");

        if (string.IsNullOrWhiteSpace(rawDescription))
        {
            rawDescription = method.CustomAttributes
                .Where(attr => string.Equals(attr.AttributeType.FullName, DescriptionAttributeTypeName, StringComparison.Ordinal))
                .Select(attr => attr.ConstructorArguments.Count == 1 ? attr.ConstructorArguments[0].Value as string : null)
                .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
        }

        return new ToolMetadata
        {
            Name = name,
            RawDescription = rawDescription,
        };
    }

    private static bool HasAttribute(IEnumerable<CustomAttributeData> attrs, string fullName)
    {
        return attrs.Any(attr => string.Equals(attr.AttributeType.FullName, fullName, StringComparison.Ordinal));
    }

    private static IReadOnlyList<Type> GetMetadataTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null).Cast<Type>().ToList();
        }
    }
}
