using System.IO;
using System.Reflection;
using ModelContextProtocol.Server;
using RevitDevTool.McpParser.Models;

namespace RevitDevTool.McpParser.Dotnet;

public static class DotnetMethodResolver
{
    private static readonly string McpToolAttributeFullName = typeof(McpServerToolAttribute).FullName!;
    private static readonly string McpPromptAttributeFullName = typeof(McpServerPromptAttribute).FullName!;
    private static readonly string McpResourceAttributeFullName = typeof(McpServerResourceAttribute).FullName!;

    public static MethodInfo? ResolveTool(McpRegisteredTool tool)
    {
        return Resolve(
            tool.ProtocolTool.Name,
            tool.Binding,
            typeof(McpServerToolTypeAttribute),
            requireAttribute: true,
            method => FindAttributeByName(method, McpToolAttributeFullName) is not null,
            method => ExtractNamedArg(FindAttributeByName(method, McpToolAttributeFullName), "Name"));
    }

    public static MethodInfo? ResolvePrompt(McpRegisteredPrompt prompt)
    {
        return Resolve(
            prompt.ProtocolPrompt.Name,
            prompt.Binding,
            typeof(McpServerPromptTypeAttribute),
            requireAttribute: false,
            _ => true,
            method => ExtractNamedArg(FindAttributeByName(method, McpPromptAttributeFullName), "Name"));
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
            method => ExtractNamedArg(FindAttributeByName(method, McpResourceAttributeFullName), "Name"));
    }

    private static CustomAttributeData? FindAttributeByName(MemberInfo member, string attributeFullName)
    {
        return member.CustomAttributes.FirstOrDefault(a =>
            string.Equals(a.AttributeType.FullName, attributeFullName, StringComparison.Ordinal));
    }

    private static string? ExtractNamedArg(CustomAttributeData? attr, string memberName)
    {
        return attr?.NamedArguments
            .Where(a => a.MemberName == memberName)
            .Select(a => a.TypedValue.Value as string)
            .FirstOrDefault();
    }

    private static MethodInfo? Resolve(
        string targetName,
        McpPrimitiveBinding binding,
        Type containerAttributeType,
        bool requireAttribute,
        Func<MethodInfo, bool> attributeChecker,
        Func<MethodInfo, string?> configuredNameSelector)
    {
        // Phase 1: Search in already-loaded assemblies (e.g. merged into main add-in)
        var result = ResolveFromLoadedAssemblies(targetName, binding, containerAttributeType,
            requireAttribute, attributeChecker, configuredNameSelector);
        if (result is not null)
            return result;

        // Phase 2: Load assembly from disk if not found (standalone tool DLLs like RevitMcpToolSet.dll)
        if (!string.IsNullOrWhiteSpace(binding.SourcePath) && File.Exists(binding.SourcePath))
        {
            try
            {
                var assembly = Assembly.LoadFrom(binding.SourcePath);
                return ResolveFromAssembly(assembly, targetName, binding, containerAttributeType,
                    requireAttribute, attributeChecker, configuredNameSelector);
            }
            catch (Exception)
            {
                // Assembly load failed (e.g. missing dependencies, wrong runtime)
                return null;
            }
        }

        return null;
    }

    private static MethodInfo? ResolveFromLoadedAssemblies(
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

            var result = ResolveFromAssembly(assembly, targetName, binding, containerAttributeType,
                requireAttribute, attributeChecker, configuredNameSelector);
            if (result is not null)
                return result;
        }

        return null;
    }

    private static MethodInfo? ResolveFromAssembly(
        Assembly assembly,
        string targetName,
        McpPrimitiveBinding binding,
        Type containerAttributeType,
        bool requireAttribute,
        Func<MethodInfo, bool> attributeChecker,
        Func<MethodInfo, string?> configuredNameSelector)
    {
        foreach (var type in MethodResolutionHelper.GetTypesWithAttribute(assembly, containerAttributeType))
        {
            if (!MethodResolutionHelper.IsContainerMatch(type, binding.ContainerType))
                continue;

            var method = FindMatchingMethod(type, targetName, binding, requireAttribute, attributeChecker, configuredNameSelector);
            if (method is not null)
                return method;
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
