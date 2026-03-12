using System.Reflection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Mcp.Parser.Models;

namespace RevitDevTool.Mcp.Parser.Dotnet;

public static class DotnetToolMethodResolver
{
    public static MethodInfo? Resolve(McpRegisteredTool tool)
    {
        var binding = tool.Binding;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                     .OrderBy(MethodResolutionHelper.GetAssemblyPath, StringComparer.OrdinalIgnoreCase))
        {
            if (!MethodResolutionHelper.IsAssemblyMatch(assembly, binding.SourcePath))
                continue;

            var method = FindToolMethodInAssembly(assembly, tool.ProtocolTool, binding);
            if (method is not null)
                return method;
        }

        return null;
    }

    private static MethodInfo? FindToolMethodInAssembly(Assembly assembly, Tool protocolTool, McpPrimitiveBinding binding)
    {
        foreach (var type in MethodResolutionHelper.GetTypesWithAttribute(assembly, typeof(McpServerToolTypeAttribute)))
        {
            if (!MethodResolutionHelper.IsContainerMatch(type, binding.ContainerType))
                continue;

            var method = FindToolMethod(type, protocolTool, binding);
            if (method is not null)
                return method;
        }

        return null;
    }

    private static MethodInfo? FindToolMethod(Type type, Tool protocolTool, McpPrimitiveBinding binding)
    {
        foreach (var method in type
                     .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                     .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
            if (toolAttr is null)
                continue;

            var toolName = !string.IsNullOrWhiteSpace(toolAttr.Name) ? toolAttr.Name : method.Name;
            if (!string.Equals(toolName, protocolTool.Name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(binding.MethodName) &&
                !string.Equals(method.Name, binding.MethodName, StringComparison.Ordinal))
                continue;

            return method;
        }

        return null;
    }
}
