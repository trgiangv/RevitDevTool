using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.McpParser;
using DevTools.McpParser.Models;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
// ReSharper disable RedundantSuppressNullableWarningExpression
namespace DevTools.McpParser.Dotnet;

public static class DotnetMcpAssemblyParser
{
    private static readonly string McpToolTypeAttributeName = typeof(McpServerToolTypeAttribute).FullName!;
    private static readonly string McpToolAttributeName = typeof(McpServerToolAttribute).FullName!;
    private static readonly string McpPromptTypeAttributeName = typeof(McpServerPromptTypeAttribute).FullName!;
    private static readonly string McpPromptAttributeName = typeof(McpServerPromptAttribute).FullName!;
    private static readonly string McpResourceTypeAttributeName = typeof(McpServerResourceTypeAttribute).FullName!;
    private static readonly string McpResourceAttributeName = typeof(McpServerResourceAttribute).FullName!;
    private static readonly string McpMetaAttributeName = typeof(McpMetaAttribute).FullName!;
    private static readonly string DescriptionAttributeTypeName = typeof(System.ComponentModel.DescriptionAttribute).FullName!;
    private static readonly string FromKeyedServicesAttributeFullName = typeof(FromKeyedServicesAttribute).FullName!;
    private static readonly string IProgressGenericFullName = typeof(IProgress<>).FullName!;
    private static readonly string RequestContextGenericFullName = typeof(RequestContext<>).FullName!;
    private static readonly string NullableGenericFullName = typeof(Nullable<>).FullName!;
    private const string IconSourceMemberName = "IconSource";
    private const string NameMemberName = "Name";
    private const string TitleMemberName = "Title";
    private const string DescriptionMemberName = "Description";
    private const string UriTemplateMemberName = "UriTemplate";
    private const string MimeTypeMemberName = "MimeType";

    public static McpRegistryCatalog ParseCatalogFromAssembly(string assemblyPath)
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
            if (HasAttribute(type.CustomAttributes, McpToolTypeAttributeName))
                tools.AddRange(ParseTools(type, assemblyPath));

            if (HasAttribute(type.CustomAttributes, McpPromptTypeAttributeName))
                prompts.AddRange(ParsePrompts(type, assemblyPath));

            if (HasAttribute(type.CustomAttributes, McpResourceTypeAttributeName))
                resources.AddRange(ParseResources(type, assemblyPath));
        }

        return new McpRegistryCatalog
        {
            Tools = tools,
            Prompts = prompts,
            Resources = resources,
        };
    }

    private static IEnumerable<McpRegisteredTool> ParseTools(Type type, string assemblyPath)
    {
        foreach (var method in GetCandidateMethods(type))
        {
            var registered = TryBuildTool(type, method, assemblyPath);
            if (registered is not null)
                yield return registered;
        }
    }

    private static IEnumerable<McpRegisteredPrompt> ParsePrompts(Type type, string assemblyPath)
    {
        foreach (var method in GetCandidateMethods(type))
        {
            var registered = TryBuildPrompt(type, method, assemblyPath);
            if (registered is not null)
                yield return registered;
        }
    }

    private static IEnumerable<McpRegisteredResource> ParseResources(Type type, string assemblyPath)
    {
        foreach (var method in GetCandidateMethods(type))
        {
            var registered = TryBuildResource(type, method, assemblyPath);
            if (registered is not null)
                yield return registered;
        }
    }

    private static IEnumerable<MethodInfo> GetCandidateMethods(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static McpRegisteredTool? TryBuildTool(Type type, MethodInfo method, string assemblyPath)
    {
        try
        {
            var toolAttribute = FindAttribute(method, McpToolAttributeName);
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

    private static McpRegisteredPrompt? TryBuildPrompt(Type type, MethodInfo method, string assemblyPath)
    {
        try
        {
            var promptAttribute = FindAttribute(method, McpPromptAttributeName);
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

    private static McpRegisteredResource? TryBuildResource(Type type, MethodInfo method, string assemblyPath)
    {
        try
        {
            var resourceAttribute = FindAttribute(method, McpResourceAttributeName);
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
            ExecutionMode.Assembly,
            assemblyPath,
            type.FullName,
            method.Name,
            sourceAddress,
            assemblyName);
    }

    private static readonly HashSet<string> InfrastructureTypeNames = new(StringComparer.Ordinal)
    {
        typeof(CancellationToken).FullName!,
        typeof(IServiceProvider).FullName!,
        typeof(McpServer).FullName!,
    };

    private static bool IsInfrastructureParameter(ParameterInfo parameter)
    {
        var paramType = parameter.ParameterType;
        var fullName = paramType.FullName ?? paramType.Name;

        if (InfrastructureTypeNames.Contains(fullName))
            return true;

        if (paramType.IsGenericType)
        {
            var genericDefFullName = paramType.GetGenericTypeDefinition().FullName;
            if (string.Equals(genericDefFullName, IProgressGenericFullName, StringComparison.Ordinal) ||
                string.Equals(genericDefFullName, RequestContextGenericFullName, StringComparison.Ordinal))
                return true;
        }

        if (parameter.CustomAttributes.Any(a =>
                string.Equals(a.AttributeType.FullName, FromKeyedServicesAttributeFullName, StringComparison.Ordinal)))
            return true;

        return false;
    }

    private static JsonElement BuildInputSchema(MethodInfo method)
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

    private static readonly Dictionary<string, string> JsonSchemaTypeMap = new(StringComparer.Ordinal)
    {
        [typeof(string).FullName!] = JsonSchemaTypeNames.String,
        [typeof(int).FullName!] = JsonSchemaTypeNames.Integer,
        [typeof(long).FullName!] = JsonSchemaTypeNames.Integer,
        [typeof(double).FullName!] = JsonSchemaTypeNames.Number,
        [typeof(float).FullName!] = JsonSchemaTypeNames.Number,
        [typeof(bool).FullName!] = JsonSchemaTypeNames.Boolean,
    };

    private static string MapParameterTypeToJsonSchema(Type parameterType)
    {
        while (true)
        {
            var fullName = parameterType.FullName ?? parameterType.Name;
            if (!parameterType.IsGenericType || parameterType.GetGenericArguments().Length <= 0) 
                return JsonSchemaTypeMap.GetValueOrDefault(fullName, JsonSchemaTypeNames.String);
            var genericDefFullName = parameterType.GetGenericTypeDefinition().FullName;
            if (!string.Equals(genericDefFullName, NullableGenericFullName, StringComparison.Ordinal)) 
                return JsonSchemaTypeMap.GetValueOrDefault(fullName, JsonSchemaTypeNames.String);
            parameterType = parameterType.GetGenericArguments()[0];
        }
    }

    private static IList<Icon>? ParseIcons(string? iconSource)
    {
        if (string.IsNullOrWhiteSpace(iconSource))
            return null;
        return [new Icon { Source = iconSource!.Trim() }];
    }

    private static PromptArgument BuildPromptArgument(ParameterInfo parameter)
    {
        return new PromptArgument
        {
            Name = parameter.Name ?? string.Empty,
            Description = ReadDescription(parameter.CustomAttributes) ?? string.Empty,
            Required = parameter is { HasDefaultValue: false, IsOptional: false },
        };
    }

    private static string BuildFallbackUriTemplate(string name, MethodInfo method)
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

    private static ToolAnnotations? BuildAnnotations(CustomAttributeData toolAttribute, string? title)
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

    private static JsonObject? BuildMeta(MethodInfo method)
    {
        var metaAttributes = method.CustomAttributes
            .Where(attr => string.Equals(attr.AttributeType.FullName, McpMetaAttributeName, StringComparison.Ordinal))
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

    private static string? ReadMetaJsonValue(CustomAttributeData attribute)
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

        return null;
    }

    private static T? ExtractNamedArg<T>(CustomAttributeData? attr, string memberName) where T : class
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

    private static T? ExtractNamedValueArg<T>(CustomAttributeData? attr, string memberName) where T : struct
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

    private static string? ReadDescription(IEnumerable<CustomAttributeData> customAttributes)
    {
        return customAttributes
            .Where(attr => string.Equals(attr.AttributeType.FullName, DescriptionAttributeTypeName, StringComparison.Ordinal))
            .Select(attr => attr.ConstructorArguments.Count == 1 ? attr.ConstructorArguments[0].Value as string : null)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
    }

    private static bool HasAttribute(IEnumerable<CustomAttributeData> attrs, string fullName)
    {
        return attrs.Any(attr => attr.AttributeType.FullName == fullName);
    }

    private static void WarnSkipped(string kind, Type type, MethodInfo method, string assemblyPath, Exception ex)
    {
        System.Diagnostics.Trace.TraceWarning(
            $"[MCP] Skip .NET {kind} '{type.FullName}.{method.Name}' in '{assemblyPath}': {ex.Message}");
    }
}
