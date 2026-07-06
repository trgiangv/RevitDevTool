using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ZLogger;
// ReSharper disable RedundantSuppressNullableWarningExpression
namespace DevTools.Mcp.Discovery;

public sealed class DotnetMcpAssemblyParser(ILogger<DotnetMcpAssemblyParser> logger)
{
    private readonly string _mcpToolTypeAttributeName = typeof(McpServerToolTypeAttribute).FullName!;
    private readonly string _mcpToolAttributeName = typeof(McpServerToolAttribute).FullName!;
    private readonly string _mcpPromptTypeAttributeName = typeof(McpServerPromptTypeAttribute).FullName!;
    private readonly string _mcpPromptAttributeName = typeof(McpServerPromptAttribute).FullName!;
    private readonly string _mcpResourceTypeAttributeName = typeof(McpServerResourceTypeAttribute).FullName!;
    private readonly string _mcpResourceAttributeName = typeof(McpServerResourceAttribute).FullName!;
    private readonly string _mcpMetaAttributeName = typeof(McpMetaAttribute).FullName!;
    private readonly string _descriptionAttributeTypeName = typeof(System.ComponentModel.DescriptionAttribute).FullName!;
    private readonly string _fromKeyedServicesAttributeFullName = typeof(FromKeyedServicesAttribute).FullName!;
    private readonly string _iProgressGenericFullName = typeof(IProgress<>).FullName!;
    private readonly string _requestContextGenericFullName = typeof(RequestContext<>).FullName!;
    private readonly string _nullableGenericFullName = typeof(Nullable<>).FullName!;

    private const string IconSourceMemberName = "IconSource";
    private const string NameMemberName = "Name";
    private const string TitleMemberName = "Title";
    private const string DescriptionMemberName = "Description";
    private const string UriTemplateMemberName = "UriTemplate";
    private const string MimeTypeMemberName = "MimeType";

    public McpRegistryCatalog ParseCatalogFromAssembly(string assemblyPath)
    {
        var tools = new List<McpRegisteredTool>();
        var prompts = new List<McpRegisteredPrompt>();
        var resources = new List<McpRegisteredResource>();
        var resolutionPaths = MetadataAssemblyPathCollector.Collect(assemblyPath);
        var resolver = new PathAssemblyResolver(resolutionPaths);
        using var metadataLoadContext = new MetadataLoadContext(resolver);
        var assembly = metadataLoadContext.LoadFromAssemblyPath(assemblyPath);

        foreach (var type in MetadataAssemblyPathCollector.GetMetadataTypes(assembly).OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase))
        {
            if (HasAttribute(type.CustomAttributes, _mcpToolTypeAttributeName))
                tools.AddRange(ParseTools(type, assemblyPath));

            if (HasAttribute(type.CustomAttributes, _mcpPromptTypeAttributeName))
                prompts.AddRange(ParsePrompts(type, assemblyPath));

            if (HasAttribute(type.CustomAttributes, _mcpResourceTypeAttributeName))
                resources.AddRange(ParseResources(type, assemblyPath));
        }

        return new McpRegistryCatalog
        {
            Tools = tools,
            Prompts = prompts,
            Resources = resources,
        };
    }

    private IEnumerable<McpRegisteredTool> ParseTools(Type type, string assemblyPath)
    {
        return GetCandidateMethods(type).Select(method => TryBuildTool(type, method, assemblyPath)).OfType<McpRegisteredTool>();
    }

    private IEnumerable<McpRegisteredPrompt> ParsePrompts(Type type, string assemblyPath)
    {
        return GetCandidateMethods(type).Select(method => TryBuildPrompt(type, method, assemblyPath)).OfType<McpRegisteredPrompt>();
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

            var name = ExtractNamedArg<string>(toolAttribute, NameMemberName) ?? method.Name;
            var title = ExtractNamedArg<string>(toolAttribute, TitleMemberName);
            var rawDescription = ExtractNamedArg<string>(toolAttribute, DescriptionMemberName) ?? ReadDescription(method.CustomAttributes);
            var description = !string.IsNullOrWhiteSpace(rawDescription)
                ? rawDescription!.Trim()
                : $"MCP tool from {type.FullName}";
            var annotations = BuildAnnotations(toolAttribute, title);
            var binding = BuildBinding(assemblyPath, type, method);
            var id = McpPrimitiveBinding.CreatePrimitiveId(name, binding.SourceAddress);

            var protocolTool = new Tool
            {
                Name = name,
                Title = title ?? name,
                Description = description,
                InputSchema = BuildInputSchema(method),
                Annotations = annotations,
                Icons = ParseIcons(ExtractNamedArg<string>(toolAttribute, IconSourceMemberName)),
                Meta = BuildMeta(method),
            };

            return new McpRegisteredTool
            {
                Id = id,
                ProtocolTool = protocolTool,
                Binding = binding,
            };
        }
        catch (Exception ex)
        {
            WarnSkipped("tool", type, method, assemblyPath, ex);
            return null;
        }
    }

    private McpRegisteredPrompt? TryBuildPrompt(Type type, MethodInfo method, string assemblyPath)
    {
        try
        {
            var promptAttribute = FindAttribute(method, _mcpPromptAttributeName);
            if (promptAttribute is null)
                return null;

            var name = ExtractNamedArg<string>(promptAttribute, NameMemberName) ?? method.Name;
            var title = ExtractNamedArg<string>(promptAttribute, TitleMemberName);
            var description = ReadDescription(method.CustomAttributes) ?? $"MCP prompt from {type.FullName}";
            var arguments = method.GetParameters()
                .Where(p => !IsInfrastructureParameter(p))
                .Select(BuildPromptArgument)
                .ToList();
            var binding = BuildBinding(assemblyPath, type, method);
            var id = McpPrimitiveBinding.CreatePrimitiveId(name, binding.SourceAddress);

            var protocolPrompt = new Prompt
            {
                Name = name,
                Title = title ?? name,
                Description = description,
                Arguments = arguments,
                Icons = ParseIcons(ExtractNamedArg<string>(promptAttribute, IconSourceMemberName)),
                Meta = BuildMeta(method),
            };

            return new McpRegisteredPrompt
            {
                Id = id,
                ProtocolPrompt = protocolPrompt,
                Binding = binding,
            };
        }
        catch (Exception ex)
        {
            WarnSkipped("prompt", type, method, assemblyPath, ex);
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

            var name = ExtractNamedArg<string>(resourceAttribute, NameMemberName) ?? method.Name;
            var title = ExtractNamedArg<string>(resourceAttribute, TitleMemberName);
            var description = ReadDescription(method.CustomAttributes) ?? $"MCP resource from {type.FullName}";
            var uriTemplate = ExtractNamedArg<string>(resourceAttribute, UriTemplateMemberName) ?? BuildFallbackUriTemplate(name, method);
            var mimeType = ExtractNamedArg<string>(resourceAttribute, MimeTypeMemberName);
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
                    Icons = ParseIcons(ExtractNamedArg<string>(resourceAttribute, IconSourceMemberName)),
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
                    Icons = ParseIcons(ExtractNamedArg<string>(resourceAttribute, IconSourceMemberName)),
                    Meta = BuildMeta(method),
                };
            }

            return new McpRegisteredResource
            {
                Id = id,
                ProtocolResource = protocolResource,
                ProtocolTemplate = protocolTemplate,
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
            var schemaType = MapParameterTypeToJsonSchema(p.ParameterType);
            var prop = new JsonObject { [IpcPropertyNames.Type] = schemaType };
            var desc = ReadDescription(p.CustomAttributes);
            if (!string.IsNullOrWhiteSpace(desc))
                prop[McpPropertyNames.Description] = desc;

            properties[p.Name ?? "arg"] = prop;
            if (p is { HasDefaultValue: false, IsOptional: false })
                required.Add(p.Name ?? "arg");
        }

        var schema = new JsonObject
        {
            [IpcPropertyNames.Type] = JsonSchemaTypeNames.Object,
            [McpPropertyNames.Properties] = properties,
        };
        if (required.Count > 0)
            schema[McpPropertyNames.Required] = required;

        return JsonSerializer.SerializeToElement(schema);
    }

    private readonly Dictionary<string, string> _jsonSchemaTypeMap = new(StringComparer.Ordinal)
    {
        [typeof(string).FullName!] = JsonSchemaTypeNames.String,
        [typeof(int).FullName!] = JsonSchemaTypeNames.Integer,
        [typeof(long).FullName!] = JsonSchemaTypeNames.Integer,
        [typeof(double).FullName!] = JsonSchemaTypeNames.Number,
        [typeof(float).FullName!] = JsonSchemaTypeNames.Number,
        [typeof(bool).FullName!] = JsonSchemaTypeNames.Boolean,
    };

    private string MapParameterTypeToJsonSchema(Type parameterType)
    {
        while (true)
        {
            var fullName = parameterType.FullName ?? parameterType.Name;
            if (!parameterType.IsGenericType || parameterType.GetGenericArguments().Length <= 0) 
                return _jsonSchemaTypeMap.GetValueOrDefault(fullName, JsonSchemaTypeNames.String);
            var genericDefFullName = parameterType.GetGenericTypeDefinition().FullName;
            if (!string.Equals(genericDefFullName, _nullableGenericFullName, StringComparison.Ordinal)) 
                return _jsonSchemaTypeMap.GetValueOrDefault(fullName, JsonSchemaTypeNames.String);
            parameterType = parameterType.GetGenericArguments()[0];
        }
    }

    private static List<Icon>? ParseIcons(string? iconSource)
    {
        if (string.IsNullOrWhiteSpace(iconSource))
            return null;
        return [new Icon { Source = iconSource!.Trim() }];
    }

    private PromptArgument BuildPromptArgument(ParameterInfo parameter)
    {
        return new PromptArgument
        {
            Name = parameter.Name ?? string.Empty,
            Description = ReadDescription(parameter.CustomAttributes) ?? string.Empty,
            Required = parameter is { HasDefaultValue: false, IsOptional: false },
        };
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

    private ToolAnnotations? BuildAnnotations(CustomAttributeData toolAttribute, string? title)
    {
        var readOnlyHint = ExtractNamedValueArg<bool>(toolAttribute, "ReadOnly");
        var destructiveHint = ExtractNamedValueArg<bool>(toolAttribute, "Destructive");
        var idempotentHint = ExtractNamedValueArg<bool>(toolAttribute, "Idempotent");
        var openWorldHint = ExtractNamedValueArg<bool>(toolAttribute, "OpenWorld");

        if (string.IsNullOrWhiteSpace(title)
            && readOnlyHint is null
            && destructiveHint is null
            && idempotentHint is null
            && openWorldHint is null)
        {
            return null;
        }

        return new ToolAnnotations
        {
            Title = title,
            ReadOnlyHint = readOnlyHint,
            DestructiveHint = destructiveHint,
            IdempotentHint = idempotentHint,
            OpenWorldHint = openWorldHint,
        };
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
        var namedJsonValue = ExtractNamedArg<string>(attribute, nameof(McpMetaAttribute.JsonValue));
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

        logger.ZLogDebug($"Failed to extract named argument '{memberName}' of type '{typeof(T).FullName}' from attribute '{attr?.AttributeType.FullName}'");
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

        logger.ZLogDebug($"Failed to extract named value argument '{memberName}' of type '{typeof(T).FullName}' from attribute '{attr?.AttributeType.FullName}'");
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
