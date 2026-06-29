using System.Reflection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ZLogger;

namespace DevTools.Mcp.Discovery;

public sealed class DotnetMethodResolver(McpToolsetContextManager contextManager, ILogger<DotnetMethodResolver> logger)
{
    private static readonly string McpToolAttributeFullName = typeof(McpServerToolAttribute).FullName!;
    private static readonly string McpPromptAttributeFullName = typeof(McpServerPromptAttribute).FullName!;
    private static readonly string McpResourceAttributeFullName = typeof(McpServerResourceAttribute).FullName!;

    public MethodInfo? ResolveTool(McpRegisteredTool tool)
    {
        return Resolve(
            tool.ProtocolTool.Name,
            tool.Binding,
            typeof(McpServerToolTypeAttribute),
            requireAttribute: true,
            method => FindAttributeByName(method, McpToolAttributeFullName) is not null,
            method => ExtractNamedArg(FindAttributeByName(method, McpToolAttributeFullName), "Name"));
    }

    public MethodInfo? ResolvePrompt(McpRegisteredPrompt prompt)
    {
        return Resolve(
            prompt.ProtocolPrompt.Name,
            prompt.Binding,
            typeof(McpServerPromptTypeAttribute),
            requireAttribute: false,
            _ => true,
            method => ExtractNamedArg(FindAttributeByName(method, McpPromptAttributeFullName), "Name"));
    }

    public MethodInfo? ResolveResource(McpRegisteredResource resource)
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
        return attr is { NamedArguments: not null }
            ? attr.NamedArguments
                .Where(a => a.MemberName == memberName)
                .Select(a => a.TypedValue.Value as string)
                .FirstOrDefault()
            : null;
    }

    private MethodInfo? Resolve(
        string targetName,
        McpPrimitiveBinding binding,
        Type containerAttributeType,
        bool requireAttribute,
        Func<MethodInfo, bool> attributeChecker,
        Func<MethodInfo, string?> configuredNameSelector)
    {
        var result = ResolveFromLoadedAssemblies(targetName, binding, containerAttributeType,
            requireAttribute, attributeChecker, configuredNameSelector);
        if (result is not null)
            return result;

        return ResolveFromToolsetContext(targetName, binding, containerAttributeType,
            requireAttribute, attributeChecker, configuredNameSelector);
    }

    private static MethodInfo? ResolveFromLoadedAssemblies(
        string targetName,
        McpPrimitiveBinding binding,
        Type containerAttributeType,
        bool requireAttribute,
        Func<MethodInfo, bool> attributeChecker,
        Func<MethodInfo, string?> configuredNameSelector)
    {
        var assemblies = AppDomain.CurrentDomain
            .GetAssemblies()
            .OrderBy(MethodResolutionHelper.GetAssemblyPath, StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assemblies)
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

    private MethodInfo? ResolveFromToolsetContext(
        string targetName,
        McpPrimitiveBinding binding,
        Type containerAttributeType,
        bool requireAttribute,
        Func<MethodInfo, bool> attributeChecker,
        Func<MethodInfo, string?> configuredNameSelector)
    {
        if (string.IsNullOrWhiteSpace(binding.SourcePath) || !File.Exists(binding.SourcePath))
            return null;

        try
        {
            var context = contextManager.GetOrCreate(binding.SourcePath);
            var assembly = context.LoadAssembly();
            return ResolveFromAssembly(assembly, targetName, binding, containerAttributeType,
                requireAttribute, attributeChecker, configuredNameSelector);
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(
                $"Failed to load toolset '{binding.SourcePath}': {ex.Message}");
            return null;
        }
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
