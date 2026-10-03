using System.Text.Json;
using System.Text.Json.Nodes;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Utils;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ZLogger;

namespace DevTools.Mcp.Discovery;

/// <summary>Parses a Python catalog emitted by the in-host driver script.</summary>
public sealed class McpPythonParser(ILogger<McpPythonParser> logger)
{
    private readonly JsonSerializerOptions catalogJsonOptions = new(JsonSerializerDefaults.Web);

    public RegistryCatalog ParseCatalogFromDirectory(
        string directory,
        Func<string, string?> parserFunction)
    {
        string? parserOutput;
        try
        {
            parserOutput = parserFunction(directory);
        }
        catch (Exception ex)
        {
            logger.ZLogError($"In-process parser failed for '{directory}': {ex.Message}\n{ex.StackTrace}");
            return RegistryCatalog.Empty;
        }

        return BuildCatalogFromOutput(parserOutput, directory);
    }

    private RegistryCatalog BuildCatalogFromOutput(string? parserOutput, string directory)
    {
        if (string.IsNullOrWhiteSpace(parserOutput))
            return RegistryCatalog.Empty;

        // ReSharper disable once RedundantSuppressNullableWarningExpression
        var catalog = DeserializeCatalog(parserOutput!, directory);
        if (catalog is null)
            return RegistryCatalog.Empty;

        return new RegistryCatalog
        {
            Tools = BuildTools(catalog.Tools, directory),
            Resources = BuildResources(catalog.Resources, directory),
        };
    }

    private PythonParsedCatalog? DeserializeCatalog(string json, string directory)
    {
        try
        {
            var catalog = JsonSerializer.Deserialize<PythonParsedCatalog>(json, catalogJsonOptions);
            if (catalog is not null) return catalog;
            logger.ZLogWarning($"Parser returned null catalog for '{directory}'.");
            return null;
        }
        catch (JsonException ex)
        {
            logger.ZLogError($"Failed to deserialize parser output for '{directory}': {ex.Message}");
            return null;
        }
    }

    private T? DeserializeSdkType<T>(JsonElement element)
    {
        return JsonSerializer.Deserialize<T>(element.GetRawText(), ToolHelpers.ProtocolOptions);
    }

    private IReadOnlyList<T> BuildEntries<TEntry, T>(
        IReadOnlyList<TEntry> entries,
        string directory,
        Func<TEntry, JsonElement> getProtocol,
        Func<TEntry, PythonBindingInfo> getBinding,
        Func<TEntry, JsonElement, PrimitiveBinding, T?> build)
        where T : class
    {
        var result = new List<T>(entries.Count);
        foreach (var entry in entries)
        {
            var binding = BuildBinding(directory, getBinding(entry));
            var item = build(entry, getProtocol(entry), binding);
            if (item is not null)
                result.Add(item);
        }
        return result;
    }

    private IReadOnlyList<RegisteredTool> BuildTools(IReadOnlyList<PythonParsedToolEntry> entries, string directory)
    {
        return BuildEntries(entries, directory,
            e => e.Protocol,
            e => e.Binding,
            (_, protocol, binding) =>
            {
                var protocolTool = TryDeserializeTool(protocol);
                if (protocolTool is null) return null;

                protocolTool.Title ??= protocolTool.Annotations?.Title;

                var id = PrimitiveBinding.CreatePrimitiveId(protocolTool.Name, binding.SourceAddress);
                return new RegisteredTool
                {
                    Id = id,
                    Descriptor = protocolTool,
                    Binding = binding,
                };
            });
    }

    private IReadOnlyList<RegisteredResource> BuildResources(IReadOnlyList<PythonParsedResourceEntry> entries, string directory)
    {
        return BuildEntries(entries, directory,
            e => e.Protocol,
            e => e.Binding,
            (entry, protocol, binding) =>
            {
                Resource? protocolResource = null;
                ResourceTemplate? protocolTemplate = null;

                if (entry.IsTemplate)
                    protocolTemplate = DeserializeSdkType<ResourceTemplate>(protocol);
                else
                    protocolResource = DeserializeSdkType<Resource>(protocol);

                var displayName = protocolTemplate?.Name ?? protocolResource?.Name ?? entry.Binding.MethodName;
                var id = PrimitiveBinding.CreatePrimitiveId(displayName, binding.SourceAddress);

                return new RegisteredResource
                {
                    Id = id,
                    Descriptor = protocolResource,
                    TemplateDescriptor = protocolTemplate,
                    Binding = binding,
                };
            });
    }

    private Tool? TryDeserializeTool(JsonElement protocol)
    {
        Tool? tool;
        try
        {
            tool = DeserializeSdkType<Tool>(protocol);
        }
        catch (JsonException ex)
        {
            logger.ZLogWarning($"Skipping tool with invalid protocol JSON: {ex.Message}");
            return null;
        }
        catch (ArgumentException)
        {
            tool = DeserializeToolWithDefaultInputSchema(protocol);
        }

        if (tool is null || string.IsNullOrWhiteSpace(tool.Name))
            return null;

        tool.InputSchema = DescriptorFactory.CoerceInputSchema(tool.InputSchema);
        return tool;
    }

    private Tool? DeserializeToolWithDefaultInputSchema(JsonElement protocol)
    {
        try
        {
            var node = JsonNode.Parse(protocol.GetRawText())!.AsObject();
            node["inputSchema"] = JsonNode.Parse("""{"type":"object"}""");
            return JsonSerializer.Deserialize<Tool>(node.ToJsonString(), ToolHelpers.ProtocolOptions);
        }
        catch (Exception ex)
        {
            logger.ZLogWarning($"Skipping tool after input schema coercion failed: {ex.Message}");
            return null;
        }
    }

    private PrimitiveBinding BuildBinding(string directory, PythonBindingInfo info)
    {
        var sourcePath = string.IsNullOrWhiteSpace(info.SourcePath) ? directory : info.SourcePath;
        var methodName = info.MethodName ?? string.Empty;
        var containerType = string.IsNullOrWhiteSpace(info.ContainerType) ? "Python" : info.ContainerType;
        var relativeModulePath = TrimPythonExtension(GetRelativeModulePath(directory, sourcePath));
        var directoryName = Path.GetFileName(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var groupName = string.Equals(directoryName, containerType, StringComparison.OrdinalIgnoreCase)
            ? directoryName
            : $"{directoryName} / {containerType}";

        return PrimitiveBinding.Create(
            ExecutionMode.Python,
            sourcePath,
            containerType,
            methodName,
            $"{containerType}:{relativeModulePath}",
            groupName);
    }

    private string GetRelativeModulePath(string rootDirectory, string sourcePath)
    {
        var root = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullSourcePath = Path.GetFullPath(sourcePath);

        return fullSourcePath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? fullSourcePath.Substring(root.Length)
            : Path.GetFileName(fullSourcePath);
    }

    private string TrimPythonExtension(string path)
    {
        return path.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
            ? path[..^3]
            : path;
    }
}
