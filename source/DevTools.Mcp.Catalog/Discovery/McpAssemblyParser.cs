using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.AssemblyIsolation.Metadata;
using DevTools.Mcp.Isolation;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ZLogger;
using SdkAttr = DevTools.Mcp.Core.Protocol.McpSpecKeys.SdkAttributes;
using SchemaKeys = DevTools.Mcp.Core.Protocol.McpSpecKeys.JsonSchema;

namespace DevTools.Mcp.Discovery;

/// <summary>
/// Reflects MCP SDK attributes from MetadataLoadContext assemblies into catalog entries.
/// </summary>
public sealed class McpAssemblyParser(ILogger<McpAssemblyParser> logger)
{
    private readonly MethodLookup _lookup = new();

    public RegistryCatalog ParseCatalogFromAssembly(string assemblyPath)
    {
        var tools = new List<RegisteredTool>();
        var resources = new List<RegisteredResource>();
        var resolutionPaths = MetadataAssemblyPathCollector.Collect(assemblyPath);
        using var metadataSession = MetadataAssemblySession.Create(assemblyPath, resolutionPaths);
        var assembly = metadataSession.LoadEntryAssembly();

        foreach (var (type, method) in _lookup.EnumerateToolMethods(assembly))
            tools.AddRange(TryBuildTool(type, method, assemblyPath) is { } tool ? [tool] : []);

        foreach (var (type, method) in _lookup.EnumerateResourceMethods(assembly))
            resources.AddRange(TryBuildResource(type, method, assemblyPath) is { } resource ? [resource] : []);

        return new RegistryCatalog
        {
            Tools = tools,
            Resources = resources,
        };
    }

    private RegisteredTool? TryBuildTool(Type type, MethodInfo method, string assemblyPath)
    {
        try
        {
            var toolAttribute = MethodLookup.FindAttribute(method, _lookup.McpToolAttributeName);
            if (toolAttribute is null)
                return null;

            var name = MethodLookup.ExtractNamedArg<string>(toolAttribute, SdkAttr.Name) ?? method.Name;
            var title = MethodLookup.ExtractNamedArg<string>(toolAttribute, SdkAttr.Title);
            var rawDescription = MethodLookup.ExtractNamedArg<string>(toolAttribute, SdkAttr.Description)
                                 ?? ReadDescription(method.CustomAttributes);
            var description = !string.IsNullOrWhiteSpace(rawDescription)
                ? rawDescription!.Trim()
                : $"MCP tool from {type.FullName}";
            var binding = MethodLookup.BuildBinding(assemblyPath, type, method);
            var id = PrimitiveBinding.CreatePrimitiveId(name, binding.SourceAddress);
            var descriptor = new Tool
            {
                Name = name,
                Title = title ?? name,
                Description = description,
                InputSchema = DescriptorFactory.CoerceInputSchema(BuildInputSchema(method)),
                OutputSchema = MethodLookup.ExtractNamedValueArg<bool>(toolAttribute, SdkAttr.UseStructuredContent) is true
                    ? JsonSerializer.SerializeToElement(McpSchemaBuilder.BuildSchema(method.ReturnType))
                    : null,
                Annotations = DescriptorFactory.BuildToolAnnotations(
                    title,
                    readOnly: MethodLookup.ExtractNamedValueArg<bool>(toolAttribute, SdkAttr.ReadOnly),
                    destructive: MethodLookup.ExtractNamedValueArg<bool>(toolAttribute, SdkAttr.Destructive),
                    idempotent: MethodLookup.ExtractNamedValueArg<bool>(toolAttribute, SdkAttr.Idempotent),
                    openWorld: MethodLookup.ExtractNamedValueArg<bool>(toolAttribute, SdkAttr.OpenWorld)),
                Meta = BuildMeta(method),
                Icons = DescriptorFactory.ParseIcons(MethodLookup.ExtractNamedArg<string>(toolAttribute, SdkAttr.IconSource)),
            };

            return new RegisteredTool
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

    private RegisteredResource? TryBuildResource(Type type, MethodInfo method, string assemblyPath)
    {
        try
        {
            var resourceAttribute = MethodLookup.FindAttribute(method, _lookup.McpResourceAttributeName);
            if (resourceAttribute is null)
                return null;

            var name = MethodLookup.ExtractNamedArg<string>(resourceAttribute, SdkAttr.Name) ?? method.Name;
            var title = MethodLookup.ExtractNamedArg<string>(resourceAttribute, SdkAttr.Title);
            var description = ReadDescription(method.CustomAttributes) ?? $"MCP resource from {type.FullName}";
            var uriTemplate = MethodLookup.ExtractNamedArg<string>(resourceAttribute, SdkAttr.UriTemplate)
                              ?? BuildFallbackUriTemplate(name, method);
            var mimeType = MethodLookup.ExtractNamedArg<string>(resourceAttribute, SdkAttr.MimeType);
            var binding = MethodLookup.BuildBinding(assemblyPath, type, method);
            var id = PrimitiveBinding.CreatePrimitiveId(name, binding.SourceAddress);
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
                    Icons = DescriptorFactory.ParseIcons(MethodLookup.ExtractNamedArg<string>(resourceAttribute, SdkAttr.IconSource)),
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
                    Icons = DescriptorFactory.ParseIcons(MethodLookup.ExtractNamedArg<string>(resourceAttribute, SdkAttr.IconSource)),
                    Meta = BuildMeta(method),
                };
            }

            return new RegisteredResource
            {
                Id = id,
                Descriptor = protocolResource,
                TemplateDescriptor = protocolTemplate,
                Binding = binding,
            };
        }
        catch (Exception ex)
        {
            WarnSkipped("resource", type, method, assemblyPath, ex);
            return null;
        }
    }

    private JsonElement BuildInputSchema(MethodInfo method)
    {
        var parameters = method.GetParameters()
            .Where(p => !_lookup.IsInfrastructureParameter(p))
            .ToList();

        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var p in parameters)
        {
            if (p.Name is not { Length: > 0 } name)
                continue;

            var prop = McpSchemaBuilder.BuildSchema(p.ParameterType, ReadDescription(p.CustomAttributes));
            properties[name] = prop;
            if (p is { HasDefaultValue: false, IsOptional: false })
                required.Add(name);
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

    private string BuildFallbackUriTemplate(string name, MethodInfo method)
    {
        var resourceName = string.IsNullOrWhiteSpace(name) ? method.Name : name;
        var parameters = method.GetParameters()
            .Where(p => !_lookup.IsInfrastructureParameter(p))
            .Select(p => p.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => $"{{{name}}}")
            .ToList();

        return parameters.Count == 0
            ? $"resource://{resourceName}"
            : $"resource://{resourceName}/{string.Join("/", parameters)}";
    }

    private JsonObject? BuildMeta(MethodInfo method)
    {
        var metaAttributes = method.CustomAttributes
            .Where(attr => string.Equals(attr.AttributeType.FullName, _lookup.McpMetaAttributeName, StringComparison.Ordinal))
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
        var namedJsonValue = MethodLookup.ExtractNamedArg<string>(attribute, SdkAttr.JsonValue);
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

    private string? ReadDescription(IEnumerable<CustomAttributeData> customAttributes) =>
        customAttributes
            .Where(attr => string.Equals(attr.AttributeType.FullName, _lookup.DescriptionAttributeTypeName, StringComparison.Ordinal))
            .Select(attr => attr.ConstructorArguments.Count == 1 ? attr.ConstructorArguments[0].Value as string : null)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

    private void WarnSkipped(string kind, Type type, MethodInfo method, string assemblyPath, Exception ex)
    {
        logger.ZLogWarning(
            $"Skip .NET {kind} '{type.FullName}.{method.Name}' in '{assemblyPath}': {ex.Message}");
    }
}
