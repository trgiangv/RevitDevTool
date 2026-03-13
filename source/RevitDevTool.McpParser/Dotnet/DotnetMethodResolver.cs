using System.Reflection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.McpParser.Models;

namespace RevitDevTool.McpParser.Dotnet;

public static class DotnetMethodResolver
{
    public static MethodInfo? ResolveTool(McpRegisteredTool tool)
    {
        return Resolve(
            tool.ProtocolTool.Name,
            tool.Binding,
            typeof(McpServerToolTypeAttribute),
            requireAttribute: true,
            method => method.GetCustomAttribute<McpServerToolAttribute>() != null,
            method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name);
    }

    public static MethodInfo? ResolvePrompt(McpRegisteredPrompt prompt)
    {
        return Resolve(
            prompt.ProtocolPrompt.Name,
            prompt.Binding,
            typeof(McpServerPromptTypeAttribute),
            requireAttribute: false,
            _ => true,
            method => method.GetCustomAttribute<McpServerPromptAttribute>()?.Name);
    }

    public static MethodInfo? ResolveResource(McpRegisteredResource resource)
    {
        var name = resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name ?? string.Empty;
        return Resolve(
            name,
            resource.Binding,
            typeof(McpServerResourceTypeAttribute),
            requireAttribute: false,
            _ => true,
            method => method.GetCustomAttribute<McpServerResourceAttribute>()?.Name);
    }

    private static MethodInfo? Resolve(
        string targetName,
        McpPrimitiveBinding binding,
        Type containerAttributeType,
        bool requireAttribute,
        Func<MethodInfo, bool> attributeChecker,
        Func<MethodInfo, string?> configuredNameSelector)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                     .OrderBy(MethodResolutionHelper.GetAssemblyPath, StringComparer.OrdinalIgnoreCase))
        {
            if (!MethodResolutionHelper.IsAssemblyMatch(assembly, binding.SourcePath))
                continue;

            foreach (var type in MethodResolutionHelper.GetTypesWithAttribute(assembly, containerAttributeType))
            {
                if (!MethodResolutionHelper.IsContainerMatch(type, binding.ContainerType))
                    continue;

                var method = FindMatchingMethod(type, targetName, binding, requireAttribute, attributeChecker, configuredNameSelector);
                if (method is not null)
                    return method;
            }
        }

        return null;
    }

    private static MethodInfo? FindMatchingMethod(
        Type type,
        string targetName,
        McpPrimitiveBinding binding,
        bool requireAttribute,
        Func<MethodInfo, bool> attributeChecker,
        Func<MethodInfo, string?> configuredNameSelector)
    {
        foreach (var method in type
                     .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                     .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (requireAttribute && !attributeChecker(method))
                continue;

            var configuredName = configuredNameSelector(method);
            var effectiveName = string.IsNullOrWhiteSpace(configuredName) ? method.Name : configuredName;
            if (!string.Equals(effectiveName, targetName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(binding.MethodName) &&
                !string.Equals(method.Name, binding.MethodName, StringComparison.Ordinal))
                continue;

            return method;
        }

        return null;
    }
}
