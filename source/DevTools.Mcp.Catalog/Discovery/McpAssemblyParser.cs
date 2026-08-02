using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ZLogger;
using SdkAttr = DevTools.Mcp.Core.Protocol.McpSpecKeys.SdkAttributes;
using SchemaKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.JsonSchema;
using IconKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.Icon;
// ReSharper disable RedundantSuppressNullableWarningExpression
namespace DevTools.Mcp.Catalog.Discovery;

public sealed class McpAssemblyParser(ILogger<McpAssemblyParser> logger)
{
    private readonly string _mcpToolTypeAttributeName = typeof(McpServerToolTypeAttribute).FullName!;
    private readonly string _mcpToolAttributeName = typeof(McpServerToolAttribute).FullName!;
    private readonly string _mcpResourceTypeAttributeName = typeof(McpServerResourceTypeAttribute).FullName!;
    private readonly string _mcpResourceAttributeName = typeof(McpServerResourceAttribute).FullName!;
    private readonly string _mcpMetaAttributeName = typeof(McpMetaAttribute).FullName!;
    private readonly string _descriptionAttributeTypeName = typeof(System.ComponentModel.DescriptionAttribute).FullName!;
    private readonly string _fromKeyedServicesAttributeFullName = typeof(FromKeyedServicesAttribute).FullName!;
    private readonly string _iProgressGenericFullName = typeof(IProgress<>).FullName!;
    private readonly string _requestContextGenericFullName = typeof(RequestContext<>).FullName!;

    public McpRegistryCatalog ParseCatalogFromAssembly(string assemblyPath)
    {
        var tools = new List<McpRegisteredTool>();
        var resources = new List<McpRegisteredResource>();
        var resolutionPaths = MetadataAssemblyPathCollector.Collect(assemblyPath);
        var resolver = new PathAssemblyResolver(resolutionPaths);
        using var metadataLoadContext = new MetadataLoadContext(resolver);
        var assembly = metadataLoadContext.LoadFromAssemblyPath(assemblyPath);

        foreach (var type in MetadataAssemblyPathCollector.GetMetadataTypes(assembly).OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase))
        {
            if (HasAttribute(type.CustomAttributes, _mcpToolTypeAttributeName))
                tools.AddRange(ParseTools(type, assemblyPath));

            if (HasAttribute(type.CustomAttributes, _mcpResourceTypeAttributeName))
                resources.AddRange(ParseResources(type, assemblyPath));
        }

        return new McpRegistryCatalog
        {
            Tools = tools,
            Resources = resources,
        };
    }

    private IEnumerable<McpRegisteredTool> ParseTools(Type type, string assemblyPath)
    {
        return GetCandidateMethods(type).Select(method => TryBuildTool(type, method, assemblyPath)).OfType<McpRegisteredTool>();
    }

    private IEnumerable<McpRegisteredResource> ParseResources(Type type, string assemblyPath)
    {
        return GetCandidateMethods(type).Select(method => TryBuildResource(type, method, assemblyPath)).OfType<McpRegisteredResource>();
    }

    private static IEnumerable<MethodInfo> GetCandidateMethods(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    private McpRegisteredTool? TryBuildTool(Type type, MethodInfo method, string assemblyPath)
    {
        try
        {
            var toolAttribute = FindAttribute(method, _mcpToolAttributeName);
            if (toolAttribute is null)
                return null;

            var name = ExtractNamedArg<string>(toolAttribute, SdkAttr.Name) ?? method.Name;
            var title = ExtractNamedArg<string>(toolAttribute, SdkAttr.Title);
            var rawDescription = ExtractNamedArg<string>(toolAttribute, SdkAttr.Description) ?? ReadDescription(method.CustomAttributes);
            var description = !string.IsNullOrWhiteSpace(rawDescription)
                ? rawDescription!.Trim()
                : $"MCP tool from {type.FullName}";
            var binding = BuildBinding(assemblyPath, type, method);
            var id = McpPrimitiveBinding.CreatePrimitiveId(name, binding.SourceAddress);
            var descriptor = new McpToolDescriptor
            {
                Name = name,
                Title = title ?? name,
                Description = description,
                InputSchema = BuildInputSchema(method),
                OutputSchema = ExtractNamedValueArg<bool>(toolAttribute, SdkAttr.UseStructuredContent) is true
                    ? JsonSerializer.SerializeToElement(new { type = SchemaKeys.Types.Object })
                    : null,
                Annotations = DescriptorFactory.ToolHints(
                    title,
                    readOnly: ExtractNamedValueArg<bool>(toolAttribute, SdkAttr.ReadOnly),
                    destructive: ExtractNamedValueArg<bool>(toolAttribute, SdkAttr.Destructive),
                    idempotent: ExtractNamedValueArg<bool>(toolAttribute, SdkAttr.Idempotent),
                    openWorld: ExtractNamedValueArg<bool>(toolAttribute, SdkAttr.OpenWorld),
                    iconSource: ExtractNamedArg<string>(toolAttribute, SdkAttr.IconSource)),
                Meta = BuildMeta(method),
                Icons = SerializeIcons(ExtractNamedArg<string>(toolAttribute, SdkAttr.IconSource)),
            };

            return new McpRegisteredTool
            {
                Id = id,
                Descriptor = descriptor,
                Binding = binding,
            };
        }
        catch (Exception ex)
        {
            WarnSkipped("tool", type, method, assemblyPath, ex);
            return null;
        }
    }

    private McpRegisteredResource? TryBuildResource(Type type, MethodInfo method, string assemblyPath)
    {
        try
        {
            var resourceAttribute = FindAttribute(method, _mcpResourceAttributeName);
            if (resourceAttribute is null)
                return null;

            var name = ExtractNamedArg<string>(resourceAttribute, SdkAttr.Name) ?? method.Name;
            var title = ExtractNamedArg<string>(resourceAttribute, SdkAttr.Title);
            var description = ReadDescription(method.CustomAttributes) ?? $"MCP resource from {type.FullName}";
            var uriTemplate = ExtractNamedArg<string>(resourceAttribute, SdkAttr.UriTemplate) ?? BuildFallbackUriTemplate(name, method);
            var mimeType = ExtractNamedArg<string>(resourceAttribute, SdkAttr.MimeType);
            var binding = BuildBinding(assemblyPath, type, method);
            var id = McpPrimitiveBinding.CreatePrimitiveId(name, binding.SourceAddress);
            var isTemplate = uriTemplate.Contains('{');

            Resource? protocolResource = null;
            ResourceTemplate? protocolTemplate = null;

            if (isTemplate)
            {
                protocolTemplate = new ResourceTemplate
                {
                    Name = name,
                    Title = title ?? name,
                    UriTemplate = uriTemplate,
                    Description = description,
                    MimeType = mimeType,
                    Icons = ParseIcons(ExtractNamedArg<string>(resourceAttribute, SdkAttr.IconSource)),
                    Meta = BuildMeta(method),
                };
            }
            else
            {
                protocolResource = new Resource
                {
                    Name = name,
                    Title = title ?? name,
                    Uri = uriTemplate,
                    Description = description,
                    MimeType = mimeType,
                    Icons = ParseIcons(ExtractNamedArg<string>(resourceAttribute, SdkAttr.IconSource)),
                    Meta = BuildMeta(method),
                };
            }

            return new McpRegisteredResource
            {
                Id = id,
                Descriptor = protocolResource is not null
                    ? DescriptorFactory.FromResource(protocolResource)
                    : null,
                TemplateDescriptor = protocolTemplate is not null
                    ? DescriptorFactory.FromTemplate(protocolTemplate)
                    : null,
                Binding = binding,
            };
        }
        catch (Exception ex)
        {
            WarnSkipped("resource", type, method, assemblyPath, ex);
            return null;
        }
    }

    private static McpPrimitiveBinding BuildBinding(string assemblyPath, Type type, MethodInfo method)
    {
        var assemblyName = Path.GetFileName(assemblyPath);
        var sourceAddress = $"{assemblyName}:{type.FullName}.{method.Name}";
        return McpPrimitiveBinding.Create(
            ExecutionMode.Dotnet,
            assemblyPath,
            type.FullName,
            method.Name,
            sourceAddress,
            assemblyName);
    }

    private readonly HashSet<string> _infrastructureTypeNames = new(StringComparer.Ordinal)
    {
        typeof(CancellationToken).FullName!,
        typeof(IServiceProvider).FullName!,
        typeof(McpServer).FullName!,
    };

    private bool IsInfrastructureParameter(ParameterInfo parameter)
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

    private JsonElement BuildInputSchema(MethodInfo method)
    {
        var parameters = method.GetParameters()
            .Where(p => !IsInfrastructureParameter(p))
            .ToList();

        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var p in parameters)
        {
            var prop = new JsonObject { [SchemaKeys.Type] = McpSchemaBuilder.FromClrType(p.ParameterType) };
            var desc = ReadDescription(p.CustomAttributes);
            if (!string.IsNullOrWhiteSpace(desc))
                prop[SchemaKeys.Description] = desc;

            properties[p.Name ?? "arg"] = prop;
            if (p is { HasDefaultValue: false, IsOptional: false })
                required.Add(p.Name ?? "arg");
        }

        var schema = new JsonObject
        {
            [SchemaKeys.Type] = SchemaKeys.Types.Object,
            [SchemaKeys.Properties] = properties,
        };
        if (required.Count > 0)
            schema[SchemaKeys.Required] = required;

        return JsonSerializer.SerializeToElement(schema);
    }

    private static JsonArray? SerializeIcons(string? iconSource)
    {
        if (string.IsNullOrWhiteSpace(iconSource))
            return null;

        return
        [
            new JsonObject { [IconKeys.Src] = iconSource!.Trim() }
        ];
    }

    private static List<Icon>? ParseIcons(string? iconSource)
    {
        if (string.IsNullOrWhiteSpace(iconSource))
            return null;
        return [new Icon { Source = iconSource!.Trim() }];
    }

    private string BuildFallbackUriTemplate(string name, MethodInfo method)
    {
        var normalizedName = string.IsNullOrWhiteSpace(name)
            ? method.Name.ToLowerInvariant()
            : name.Trim().Replace(' ', '-').ToLowerInvariant();
        var parameters = method.GetParameters()
            .Where(p => !IsInfrastructureParameter(p))
            .Select(p => $"{{{p.Name}}}")
            .ToList();

        return parameters.Count == 0
            ? $"resource://{normalizedName}"
            : $"resource://{normalizedName}/{string.Join("/", parameters)}";
    }

    private static CustomAttributeData? FindAttribute(MemberInfo member, string attributeFullName)
    {
        return member.CustomAttributes.FirstOrDefault(attr => attr.AttributeType.FullName == attributeFullName);
    }

    private JsonObject? BuildMeta(MethodInfo method)
    {
        var metaAttributes = method.CustomAttributes
            .Where(attr => string.Equals(attr.AttributeType.FullName, _mcpMetaAttributeName, StringComparison.Ordinal))
            .ToList();
        if (metaAttributes.Count == 0)
            return null;

        var metadata = new JsonObject();
        foreach (var attribute in metaAttributes)
        {
            var name = attribute.ConstructorArguments.Count > 0 ? attribute.ConstructorArguments[0].Value as string : null;
            var jsonValue = ReadMetaJsonValue(attribute);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(jsonValue))
                continue;

            metadata[name!] = JsonNode.Parse(jsonValue!);
        }

        return metadata.Count == 0 ? null : metadata;
    }

    private string? ReadMetaJsonValue(CustomAttributeData attribute)
    {
        var namedJsonValue = ExtractNamedArg<string>(attribute, SdkAttr.JsonValue);
        if (!string.IsNullOrWhiteSpace(namedJsonValue))
            return namedJsonValue;

        if (attribute.ConstructorArguments.Count > 1 && attribute.ConstructorArguments[1].Value is not null)
        {
            var value = attribute.ConstructorArguments[1].Value;
            return value switch
            {
                string text => JsonSerializer.Serialize(text),
                bool flag => JsonSerializer.Serialize(flag),
                double number => JsonSerializer.Serialize(number),
                _ => JsonSerializer.Serialize(value)
            };
        }

        logger.ZLogDebug($"Failed to read JSON value from attribute '{attribute.AttributeType.FullName}'");
        return null;
    }

    private T? ExtractNamedArg<T>(CustomAttributeData? attr, string memberName) where T : class
    {
        var namedAgrs = attr?.NamedArguments;
        if (namedAgrs == null) return null;
        foreach (var namedArg in namedAgrs)
        {
            if (namedArg.MemberName == memberName && namedArg.TypedValue.Value is T value)
                return value;
        }

        return null;
    }

    private T? ExtractNamedValueArg<T>(CustomAttributeData? attr, string memberName) where T : struct
    {
        var namedAgrs = attr?.NamedArguments;
        if (namedAgrs == null) return null;
        foreach (var namedArg in namedAgrs)
        {
            if (namedArg.MemberName == memberName && namedArg.TypedValue.Value is T value)
                return value;
        }

        return null;
    }

    private string? ReadDescription(IEnumerable<CustomAttributeData> customAttributes)
    {
        return customAttributes
            .Where(attr => string.Equals(attr.AttributeType.FullName, _descriptionAttributeTypeName, StringComparison.Ordinal))
            .Select(attr => attr.ConstructorArguments.Count == 1 ? attr.ConstructorArguments[0].Value as string : null)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
    }

    private static bool HasAttribute(IEnumerable<CustomAttributeData> attrs, string fullName)
    {
        return attrs.Any(attr => attr.AttributeType.FullName == fullName);
    }

    private void WarnSkipped(string kind, Type type, MethodInfo method, string assemblyPath, Exception ex)
    {
        logger.ZLogWarning(
            $"Skip .NET {kind} '{type.FullName}.{method.Name}' in '{assemblyPath}': {ex.Message}");
    }
}
