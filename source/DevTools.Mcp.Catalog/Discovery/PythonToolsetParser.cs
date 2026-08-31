using System.Text.Json;
using System.Text.Json.Nodes;
using CliWrap;
using CliWrap.Buffered;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ZLogger;

namespace DevTools.Mcp.Catalog.Discovery;

public sealed class PythonToolsetParser(ILogger<PythonToolsetParser> logger)
{
    private readonly JsonSerializerOptions catalogJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly JsonSerializerOptions sdkJsonOptions = McpJsonUtilities.DefaultOptions;

    public McpRegistryCatalog ParseDirectoryCatalog(
        string toolsetDirectory,
        string pythonExecutablePath,
        string parserScriptPath)
    {
        if (string.IsNullOrWhiteSpace(pythonExecutablePath))
            throw new ArgumentException(@"Python executable path is required.", nameof(pythonExecutablePath));

        var parserOutput = RunParserProcess(toolsetDirectory, pythonExecutablePath, parserScriptPath);
        return BuildCatalogFromOutput(parserOutput, toolsetDirectory);
    }

    public McpRegistryCatalog ParseDirectoryCatalog(
        string toolsetDirectory,
        Func<string, string?> parserFunction)
    {
        string? parserOutput;
        try
        {
            parserOutput = parserFunction(toolsetDirectory);
        }
        catch (Exception ex)
        {
            logger.ZLogError($"In-process parser failed for '{toolsetDirectory}': {ex.Message}\n{ex.StackTrace}");
            return McpRegistryCatalog.Empty;
        }

        return BuildCatalogFromOutput(parserOutput, toolsetDirectory);
    }

    private McpRegistryCatalog BuildCatalogFromOutput(string? parserOutput, string toolsetDirectory)
    {
        if (string.IsNullOrWhiteSpace(parserOutput))
            return McpRegistryCatalog.Empty;

        // ReSharper disable once RedundantSuppressNullableWarningExpression
        var catalog = DeserializeCatalog(parserOutput!, toolsetDirectory);
        if (catalog is null)
            return McpRegistryCatalog.Empty;

        return new McpRegistryCatalog
        {
            Tools = NormalizeTools(catalog.Tools, toolsetDirectory),
            Resources = NormalizeResources(catalog.Resources, toolsetDirectory),
        };
    }

    private string? RunParserProcess(
        string directory,
        string pythonExecutablePath,
        string parserScriptPath)
    {
        try
        {
            var result = Cli.Wrap(pythonExecutablePath)
                .WithArguments([parserScriptPath, directory])
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync()
                .GetAwaiter()
                .GetResult();

            if (result.ExitCode != 0)
            {
                logger.ZLogError(
                    $"Python parser execution failed for '{directory}' with exit code {result.ExitCode}: {result.StandardError}");
                return null;
            }

            return result.StandardOutput.Trim();
        }
        catch (Exception ex)
        {
            logger.ZLogError($"Unexpected parser failure for '{directory}': {ex.Message}");
            return null;
        }
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
        return JsonSerializer.Deserialize<T>(element.GetRawText(), sdkJsonOptions);
    }

    private IReadOnlyList<T> NormalizeEntries<TEntry, T>(
        IReadOnlyList<TEntry> entries,
        string directory,
        Func<TEntry, JsonElement> getProtocol,
        Func<TEntry, PythonBindingInfo> getBinding,
        Func<TEntry, JsonElement, McpPrimitiveBinding, T?> build)
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

    private IReadOnlyList<McpRegisteredTool> NormalizeTools(IReadOnlyList<PythonParsedToolEntry> entries, string directory)
    {
        return NormalizeEntries(entries, directory,
            e => e.Protocol,
            e => e.Binding,
            (_, protocol, binding) =>
            {
                var protocolTool = TryDeserializeTool(protocol);
                if (protocolTool is null) return null;

                protocolTool.Title ??= protocolTool.Annotations?.Title;

                var id = McpPrimitiveBinding.CreatePrimitiveId(protocolTool.Name, binding.SourceAddress);
                return new McpRegisteredTool
                {
                    Id = id,
                    Descriptor = protocolTool,
                    Binding = binding,
                };
            });
    }

    private IReadOnlyList<McpRegisteredResource> NormalizeResources(IReadOnlyList<PythonParsedResourceEntry> entries, string directory)
    {
        return NormalizeEntries(entries, directory,
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
                var id = McpPrimitiveBinding.CreatePrimitiveId(displayName, binding.SourceAddress);

                return new McpRegisteredResource
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

        return DescriptorFactory.NormalizeTool(tool);
    }

    private Tool? DeserializeToolWithDefaultInputSchema(JsonElement protocol)
    {
        try
        {
            var node = JsonNode.Parse(protocol.GetRawText())!.AsObject();
            node["inputSchema"] = JsonNode.Parse("""{"type":"object"}""");
            return JsonSerializer.Deserialize<Tool>(node.ToJsonString(), sdkJsonOptions);
        }
        catch (Exception ex)
        {
            logger.ZLogWarning($"Skipping tool after input schema coercion failed: {ex.Message}");
            return null;
        }
    }

    private McpPrimitiveBinding BuildBinding(string toolsetDirectory, PythonBindingInfo info)
    {
        var sourcePath = string.IsNullOrWhiteSpace(info.SourcePath) ? toolsetDirectory : info.SourcePath;
        var methodName = string.IsNullOrWhiteSpace(info.MethodName) ? "unknown" : info.MethodName;
        var containerType = string.IsNullOrWhiteSpace(info.ContainerType) ? "PythonToolSet" : info.ContainerType;
        var relativeModulePath = TrimPythonExtension(GetRelativeModulePath(toolsetDirectory, sourcePath)
            .Replace('\\', '/'));
        var toolsetName = Path.GetFileName(Path.GetFullPath(toolsetDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var groupName = string.Equals(toolsetName, containerType, StringComparison.OrdinalIgnoreCase)
            ? toolsetName
            : $"{toolsetName} / {containerType}";

        return McpPrimitiveBinding.Create(
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
