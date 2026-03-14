using System.Diagnostics;
using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using RevitDevTool.McpParser.Models;

namespace RevitDevTool.McpParser.Python;

public static class PythonToolsetParser
{
    private static readonly JsonSerializerOptions CatalogJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions SdkJsonOptions = McpJsonUtilities.DefaultOptions;

    public static McpRegistryCatalog ParseDirectoryCatalog(string toolsetDirectory, string pythonExecutablePath, string parserScriptPath)
    {
        if (string.IsNullOrWhiteSpace(pythonExecutablePath))
            throw new ArgumentException("Python executable path is required.", nameof(pythonExecutablePath));

        var parserOutput = RunParserProcess(toolsetDirectory, pythonExecutablePath, parserScriptPath);
        return BuildCatalogFromOutput(parserOutput, toolsetDirectory);
    }

    public static McpRegistryCatalog ParseDirectoryCatalog(string toolsetDirectory, Func<string, string?> parserFunction)
    {
        string? parserOutput;
        try
        {
            parserOutput = parserFunction(toolsetDirectory);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[MCP] In-process parser failed for '{toolsetDirectory}': {ex.Message}\n{ex.StackTrace}");
            return McpRegistryCatalog.Empty;
        }
    
        return BuildCatalogFromOutput(parserOutput, toolsetDirectory);
    }

    private static McpRegistryCatalog BuildCatalogFromOutput(string? parserOutput, string toolsetDirectory)
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
            Prompts = NormalizePrompts(catalog.Prompts, toolsetDirectory),
            Resources = NormalizeResources(catalog.Resources, toolsetDirectory),
        };
    }

    private static string? RunParserProcess(string directory, string pythonExecutablePath, string parserScriptPath)
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
                Trace.TraceError(
                    $"[MCP] Python parser execution failed for '{directory}' with exit code {result.ExitCode}: {result.StandardError}");
                return null;
            }

            return result.StandardOutput.Trim();
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[MCP] Unexpected parser failure for '{directory}': {ex.Message}");
            return null;
        }
    }

    private static PythonParsedCatalog? DeserializeCatalog(string json, string directory)
    {
        try
        {
            var catalog = JsonSerializer.Deserialize<PythonParsedCatalog>(json, CatalogJsonOptions);
            if (catalog is not null) return catalog;
            Trace.TraceWarning($"[MCP] Parser returned null catalog for '{directory}'.");
            return null;
        }
        catch (JsonException ex)
        {
            Trace.TraceError($"[MCP] Failed to deserialize parser output for '{directory}': {ex.Message}");
            return null;
        }
    }

    private static T? DeserializeSdkType<T>(JsonElement element)
    {
        return JsonSerializer.Deserialize<T>(element.GetRawText(), SdkJsonOptions);
    }

    private static IReadOnlyList<T> NormalizeEntries<TEntry, T>(
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

    private static IReadOnlyList<McpRegisteredTool> NormalizeTools(IReadOnlyList<PythonParsedToolEntry> entries, string directory)
    {
        return NormalizeEntries(entries, directory,
            e => e.Protocol,
            e => e.Binding,
            (_, protocol, binding) =>
            {
                var protocolTool = DeserializeSdkType<Tool>(protocol);
                if (protocolTool is null) return null;

                protocolTool.Title ??= protocolTool.Annotations?.Title;

                var id = McpPrimitiveBinding.CreatePrimitiveId(protocolTool.Name, binding.SourceAddress);
                return new McpRegisteredTool
                {
                    Id = id,
                    ProtocolTool = protocolTool,
                    Binding = binding,
                };
            });
    }

    private static IReadOnlyList<McpRegisteredPrompt> NormalizePrompts(IReadOnlyList<PythonParsedPromptEntry> entries, string directory)
    {
        return NormalizeEntries(entries, directory,
            e => e.Protocol,
            e => e.Binding,
            (_, protocol, binding) =>
            {
                var protocolPrompt = DeserializeSdkType<Prompt>(protocol);
                if (protocolPrompt is null) return null;

                var id = McpPrimitiveBinding.CreatePrimitiveId(protocolPrompt.Name, binding.SourceAddress);
                return new McpRegisteredPrompt
                {
                    Id = id,
                    ProtocolPrompt = protocolPrompt,
                    Binding = binding,
                };
            });
    }

    private static IReadOnlyList<McpRegisteredResource> NormalizeResources(IReadOnlyList<PythonParsedResourceEntry> entries, string directory)
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
                    ProtocolResource = protocolResource,
                    ProtocolTemplate = protocolTemplate,
                    Binding = binding,
                };
            });
    }

    private static McpPrimitiveBinding BuildBinding(string toolsetDirectory, PythonBindingInfo info)
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

    private static string GetRelativeModulePath(string rootDirectory, string sourcePath)
    {
        var root = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullSourcePath = Path.GetFullPath(sourcePath);

        return fullSourcePath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? fullSourcePath.Substring(root.Length)
            : Path.GetFileName(fullSourcePath);
    }

    private static string TrimPythonExtension(string path)
    {
        return path.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
            ? path[..^3]
            : path;
    }
}
