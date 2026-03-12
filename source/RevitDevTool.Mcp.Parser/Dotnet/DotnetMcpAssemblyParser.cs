using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Contracts;
using RevitDevTool.Mcp.Parser.Models;
// ReSharper disable RedundantSuppressNullableWarningExpression
namespace RevitDevTool.Mcp.Parser.Dotnet;

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

            var name = ExtractNamedArg<string>(toolAttribute, "Name") ?? method.Name;
            var title = ExtractNamedArg<string>(toolAttribute, "Title");
            var rawDescription = ExtractNamedArg<string>(toolAttribute, "Description") ?? ReadDescription(method);
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
                InputSchema = ParseSchema(BuildInputSchema(method)),
                Annotations = annotations,
                Icons = ParseIcons(ExtractNamedArg<string>(toolAttribute, IconSourceMemberName)),
                Meta = ParseMeta(BuildMetaJson(method)),
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

            var name = ExtractNamedArg<string>(promptAttribute, "Name") ?? method.Name;
            var title = ExtractNamedArg<string>(promptAttribute, "Title");
            var description = ReadDescription(method) ?? $"MCP prompt from {type.FullName}";
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
                Meta = ParseMeta(BuildMetaJson(method)),
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

            var name = ExtractNamedArg<string>(resourceAttribute, "Name") ?? method.Name;
            var title = ExtractNamedArg<string>(resourceAttribute, "Title");
            var description = ReadDescription(method) ?? $"MCP resource from {type.FullName}";
            var uriTemplate = ExtractNamedArg<string>(resourceAttribute, "UriTemplate") ?? BuildFallbackUriTemplate(name, method);
            var mimeType = ExtractNamedArg<string>(resourceAttribute, "MimeType");
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
                    Meta = ParseMeta(BuildMetaJson(method)),
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
                    Meta = ParseMeta(BuildMetaJson(method)),
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
        var assemblyName = Path.GetFileName(assemblyPath) ?? assemblyPath;
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

    private static string BuildInputSchema(MethodInfo method)
    {
        var parameters = method.GetParameters()
            .Where(p => !IsInfrastructureParameter(p))
            .ToList();

        var properties = new Dictionary<string, object>(StringComparer.Ordinal);
        var required = new List<string>();

        foreach (var p in parameters)
        {
            var schemaType = MapParameterTypeToJsonSchema(p.ParameterType);
            var prop = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["type"] = schemaType,
            };
            var desc = ReadDescription(p);
            if (!string.IsNullOrWhiteSpace(desc))
                prop["description"] = desc!;

            properties[p.Name ?? "arg"] = prop;
            if (!p.HasDefaultValue && !p.IsOptional)
                required.Add(p.Name ?? "arg");
        }

        var schema = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["type"] = "object",
            ["properties"] = properties,
        };
        if (required.Count > 0)
            schema["required"] = required;

        return JsonSerializer.Serialize(schema);
    }

    private static readonly Dictionary<string, string> JsonSchemaTypeMap = new(StringComparer.Ordinal)
    {
        [typeof(string).FullName!] = "string",
        [typeof(int).FullName!] = "integer",
        [typeof(long).FullName!] = "integer",
        [typeof(double).FullName!] = "number",
        [typeof(float).FullName!] = "number",
        [typeof(bool).FullName!] = "boolean",
    };

    private static string MapParameterTypeToJsonSchema(Type parameterType)
    {
        var fullName = parameterType.FullName ?? parameterType.Name;
        if (parameterType.IsGenericType && parameterType.GetGenericArguments().Length > 0)
        {
            var genericDefFullName = parameterType.GetGenericTypeDefinition().FullName;
            if (string.Equals(genericDefFullName, NullableGenericFullName, StringComparison.Ordinal))
                return MapParameterTypeToJsonSchema(parameterType.GetGenericArguments()[0]);
        }

        return JsonSchemaTypeMap.TryGetValue(fullName, out var schemaType) ? schemaType : "string";
    }

    private static JsonElement ParseSchema(string schemaJson)
    {
        using var doc = JsonDocument.Parse(schemaJson);
        return doc.RootElement.Clone();
    }

    private static IList<Icon>? ParseIcons(string? iconSource)
    {
        if (string.IsNullOrWhiteSpace(iconSource))
            return null;
        return [new Icon { Source = iconSource!.Trim() }];
    }

    private static JsonObject? ParseMeta(string? metaJson)
    {
        if (string.IsNullOrWhiteSpace(metaJson))
            return null;
        try
        {
            return JsonNode.Parse(metaJson!) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static PromptArgument BuildPromptArgument(ParameterInfo parameter)
    {
        return new PromptArgument
        {
            Name = parameter.Name ?? string.Empty,
            Description = ReadDescription(parameter) ?? string.Empty,
            Required = !parameter.HasDefaultValue && !parameter.IsOptional,
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

    private static string? BuildMetaJson(MethodInfo method)
    {
        var metaAttributes = method.CustomAttributes
            .Where(attr => string.Equals(attr.AttributeType.FullName, McpMetaAttributeName, StringComparison.Ordinal))
            .ToList();
        if (metaAttributes.Count == 0)
            return null;

        var metadata = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var attribute in metaAttributes)
        {
            var name = attribute.ConstructorArguments.Count > 0 ? attribute.ConstructorArguments[0].Value as string : null;
            var jsonValue = ReadMetaJsonValue(attribute);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(jsonValue))
                continue;

            using var document = JsonDocument.Parse(jsonValue!);
            metadata[name!] = document.RootElement.Clone();
        }

        return metadata.Count == 0 ? null : JsonSerializer.Serialize(metadata);
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

    private static T? ExtractNamedArg<T>(CustomAttributeData attr, string memberName) where T : class
    {
        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg.MemberName == memberName && namedArg.TypedValue.Value is T value)
                return value;
        }

        return null;
    }

    private static T? ExtractNamedValueArg<T>(CustomAttributeData attr, string memberName) where T : struct
    {
        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg.MemberName == memberName && namedArg.TypedValue.Value is T value)
                return value;
        }

        return null;
    }

    private static string? ReadDescription(MemberInfo member)
    {
        return member.CustomAttributes
            .Where(attr => string.Equals(attr.AttributeType.FullName, DescriptionAttributeTypeName, StringComparison.Ordinal))
            .Select(attr => attr.ConstructorArguments.Count == 1 ? attr.ConstructorArguments[0].Value as string : null)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
    }

    private static string? ReadDescription(ParameterInfo parameter)
    {
        return parameter.CustomAttributes
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
