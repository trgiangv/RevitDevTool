using System.Reflection;
using DevTools.AssemblyIsolation.Metadata;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.Isolation;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Discovery;

/// <summary>
/// MetadataLoadContext attribute matching (by FullName) and method enumeration for MCP discovery.
/// </summary>
public sealed class MethodLookup
{
    private readonly string _mcpToolTypeAttributeName = typeof(McpServerToolTypeAttribute).FullName!;
    private readonly string _mcpResourceTypeAttributeName = typeof(McpServerResourceTypeAttribute).FullName!;
    private readonly string _mcpToolAttributeName = typeof(McpServerToolAttribute).FullName!;
    private readonly string _mcpResourceAttributeName = typeof(McpServerResourceAttribute).FullName!;
    private readonly string _fromKeyedServicesAttributeFullName = typeof(FromKeyedServicesAttribute).FullName!;
    private readonly string _iProgressGenericFullName = typeof(IProgress<>).FullName!;
    private readonly string _requestContextGenericFullName = typeof(RequestContext<>).FullName!;

    private readonly HashSet<string> _infrastructureTypeNames = new(StringComparer.Ordinal)
    {
        typeof(CancellationToken).FullName!,
        typeof(IServiceProvider).FullName!,
        typeof(McpServer).FullName!,
    };

    public static CustomAttributeData? FindAttribute(MemberInfo member, string attributeFullName) =>
        member.CustomAttributes.FirstOrDefault(attr => attr.AttributeType.FullName == attributeFullName);

    public static bool HasAttribute(IEnumerable<CustomAttributeData> attrs, string fullName) =>
        attrs.Any(attr => attr.AttributeType.FullName == fullName);

    public static IEnumerable<MethodInfo> GetCandidateMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);

    public static T? ExtractNamedArg<T>(CustomAttributeData? attr, string memberName) where T : class
    {
        var namedArgs = attr?.NamedArguments;
        if (namedArgs == null)
            return null;
        foreach (var namedArg in namedArgs)
        {
            if (namedArg.MemberName == memberName && namedArg.TypedValue.Value is T value)
                return value;
        }

        return null;
    }

    public static T? ExtractNamedValueArg<T>(CustomAttributeData? attr, string memberName) where T : struct
    {
        var namedArgs = attr?.NamedArguments;
        if (namedArgs == null)
            return null;
        foreach (var namedArg in namedArgs)
        {
            if (namedArg.MemberName == memberName && namedArg.TypedValue.Value is T value)
                return value;
        }

        return null;
    }

    public static PrimitiveBinding BuildBinding(string assemblyPath, Type type, MethodInfo method)
    {
        var assemblyName = Path.GetFileName(assemblyPath);
        var sourceAddress = $"{assemblyName}:{type.FullName}.{method.Name}";
        return PrimitiveBinding.Create(
            ExecutionMode.Dotnet,
            assemblyPath,
            type.FullName,
            method.Name,
            sourceAddress,
            assemblyName);
    }

    public IEnumerable<(Type ContainerType, MethodInfo Method)> EnumerateToolMethods(Assembly assembly)
    {
        foreach (var type in MetadataAssemblyPathCollector.GetMetadataTypes(assembly)
                     .OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase))
        {
            if (!HasAttribute(type.CustomAttributes, _mcpToolTypeAttributeName))
                continue;

            foreach (var method in GetCandidateMethods(type))
            {
                if (FindAttribute(method, _mcpToolAttributeName) is null)
                    continue;

                yield return (type, method);
            }
        }
    }

    public IEnumerable<(Type ContainerType, MethodInfo Method)> EnumerateResourceMethods(Assembly assembly)
    {
        foreach (var type in MetadataAssemblyPathCollector.GetMetadataTypes(assembly)
                     .OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase))
        {
            if (!HasAttribute(type.CustomAttributes, _mcpResourceTypeAttributeName))
                continue;

            foreach (var method in GetCandidateMethods(type))
            {
                if (FindAttribute(method, _mcpResourceAttributeName) is null)
                    continue;

                yield return (type, method);
            }
        }
    }

    public bool IsInfrastructureParameter(ParameterInfo parameter)
    {
        var paramType = parameter.ParameterType;
        var fullName = paramType.FullName ?? paramType.Name;

        if (_infrastructureTypeNames.Contains(fullName))
            return true;

        if (paramType.IsGenericType)
        {
            var genericDefFullName = paramType.GetGenericTypeDefinition().FullName;
            if (string.Equals(genericDefFullName, _iProgressGenericFullName, StringComparison.Ordinal) ||
                string.Equals(genericDefFullName, _requestContextGenericFullName, StringComparison.Ordinal))
                return true;
        }

        return parameter.CustomAttributes.Any(a =>
            string.Equals(a.AttributeType.FullName, _fromKeyedServicesAttributeFullName, StringComparison.Ordinal));
    }

    internal string McpToolAttributeName => _mcpToolAttributeName;
    internal string McpResourceAttributeName => _mcpResourceAttributeName;
    internal string McpMetaAttributeName => typeof(McpMetaAttribute).FullName!;
    internal string DescriptionAttributeTypeName => typeof(System.ComponentModel.DescriptionAttribute).FullName!;
}
