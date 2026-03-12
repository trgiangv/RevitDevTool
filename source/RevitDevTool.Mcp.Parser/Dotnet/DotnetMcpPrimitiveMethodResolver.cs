using System.Reflection;
using ModelContextProtocol.Server;
using RevitDevTool.Mcp.Parser.Models;

namespace RevitDevTool.Mcp.Parser.Dotnet;

public static class DotnetMcpPrimitiveMethodResolver
{
    public static MethodInfo? ResolvePrompt(McpRegisteredPrompt prompt)
    {
        return ResolvePrimitive(
            prompt.ProtocolPrompt.Name,
            prompt.Binding,
            typeof(McpServerPromptTypeAttribute),
            method => method.GetCustomAttribute<McpServerPromptAttribute>()?.Name);
    }

    public static MethodInfo? ResolveResource(McpRegisteredResource resource)
    {
        var name = resource.ProtocolResource?.Name ?? resource.ProtocolTemplate?.Name ?? string.Empty;
        return ResolvePrimitive(
            name,
            resource.Binding,
            typeof(McpServerResourceTypeAttribute),
            method => method.GetCustomAttribute<McpServerResourceAttribute>()?.Name);
    }

    private static MethodInfo? ResolvePrimitive(
        string primitiveName,
        McpPrimitiveBinding binding,
        Type containerAttributeType,
        Func<MethodInfo, string?> configuredNameSelector)
    {
        var candidateTypes = AppDomain.CurrentDomain.GetAssemblies()
            .OrderBy(MethodResolutionHelper.GetAssemblyPath, StringComparer.OrdinalIgnoreCase)
            .Where(a => MethodResolutionHelper.IsAssemblyMatch(a, binding.SourcePath))
            .SelectMany(a => MethodResolutionHelper.GetTypesWithAttribute(a, containerAttributeType))
            .Where(t => MethodResolutionHelper.IsContainerMatch(t, binding.ContainerType));

        foreach (var type in candidateTypes)
        {
            var method = FindMatchingMethod(type, primitiveName, binding, configuredNameSelector);
            if (method is not null)
                return method;
        }

        return null;
    }

    private static MethodInfo? FindMatchingMethod(
        Type type,
        string primitiveName,
        McpPrimitiveBinding binding,
        Func<MethodInfo, string?> configuredNameSelector)
    {
        foreach (var method in type
                     .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                     .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var configuredName = configuredNameSelector(method);
            if (configuredName is null && !IsMethodNameMatch(method, binding.MethodName, primitiveName))
                continue;

            var effectiveName = string.IsNullOrWhiteSpace(configuredName) ? method.Name : configuredName;
            if (!string.Equals(effectiveName, primitiveName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(binding.MethodName) &&
                !string.Equals(method.Name, binding.MethodName, StringComparison.Ordinal))
                continue;

            return method;
        }

        return null;
    }

    private static bool IsMethodNameMatch(MethodInfo method, string? methodName, string primitiveName)
    {
        return !string.IsNullOrWhiteSpace(methodName) 
            ? string.Equals(method.Name, methodName, StringComparison.Ordinal) 
            : string.Equals(method.Name, primitiveName, StringComparison.OrdinalIgnoreCase);
    }
}
