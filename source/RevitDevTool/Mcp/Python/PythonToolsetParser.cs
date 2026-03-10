using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Python.Runtime;
using RevitDevTool.Contracts;
using RevitDevTool.Execution.Providers.Python;

namespace RevitDevTool.Mcp.Python;

public sealed class PythonToolsetParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyList<McpToolDefinition> ParseDirectory(string toolsetDirectory)
    {
        PythonInitializer.InitializeAsync().GetAwaiter().GetResult();

        var parserOutput = RunInProcess(toolsetDirectory);
        if (parserOutput is null)
            return [];

        var tools = DeserializeTools(parserOutput, toolsetDirectory);
        if (tools is null)
            return [];

        NormalizeToolDefinitions(tools, toolsetDirectory);
        return tools;
    }

    private static string? RunInProcess(string directory)
    {
        try
        {
            using (Py.GIL())
            {
                if (PythonInitializer.GlobalScope is null)
                {
                    Trace.TraceWarning("[MCP] Python global scope not initialized. Cannot parse tools.");
                    return null;
                }

                using var scope = PythonInitializer.GlobalScope.NewScope();
                PythonExecutor.PrepareExecutionScope(scope, directory, directory);

                scope.Exec(PythonEmbedded.ToolParserScript);

                scope.Set("__toolset_dir__", new PyString(directory));
                scope.Exec("__parser_result__ = parse_directory(__toolset_dir__)");

                var result = scope.Get("__parser_result__").As<string>();
                return result;
            }
        }
        catch (PythonException ex)
        {
            Trace.TraceWarning($"[MCP] In-process Python parser failed for '{directory}': {ex.Message}\n{ex.StackTrace}");
            return null;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[MCP] In-process Python parser error for '{directory}': {ex.Message}");
            return null;
        }
    }

    private static List<McpToolDefinition>? DeserializeTools(string json, string directory)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<McpToolDefinition>>(json, JsonOptions);
            if (items is null)
            {
                Trace.TraceWarning($"[MCP] Parser returned null tool list for '{directory}'.");
            }
            return items;
        }
        catch (JsonException ex)
        {
            Trace.TraceWarning($"[MCP] Parser JSON deserialize failed for '{directory}': {ex.Message}.");
            return null;
        }
    }

    private static void NormalizeToolDefinitions(List<McpToolDefinition> tools, string directory)
    {
        foreach (var item in tools)
        {
            item.SourceKind = ExecutionMode.Python;
            item.SourcePath = string.IsNullOrWhiteSpace(item.SourcePath) ? directory : item.SourcePath;
            item.MethodName = string.IsNullOrWhiteSpace(item.MethodName) ? item.Name : item.MethodName;
            item.ContainerType = string.IsNullOrWhiteSpace(item.ContainerType) ? "PythonToolSet" : item.ContainerType;
            var relativeModulePath = TrimPythonExtension(GetRelativeModulePath(directory, item.SourcePath)
                .Replace('\\', '/'));
            var toolsetName = Path.GetFileName(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            item.SourceAddress = $"{item.ContainerType}:{relativeModulePath}";
            item.GroupKey = $"{Path.GetFullPath(directory)}::{item.ContainerType}";
            item.GroupName = string.Equals(toolsetName, item.ContainerType, StringComparison.OrdinalIgnoreCase)
                ? toolsetName
                : $"{toolsetName} / {item.ContainerType}";
        }
    }

    private static string GetRelativeModulePath(string rootDirectory, string sourcePath)
    {
        var normalizedRoot = AppendDirectorySeparator(Path.GetFullPath(rootDirectory));
        var normalizedSource = Path.GetFullPath(sourcePath);
        return normalizedSource.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            ? normalizedSource[normalizedRoot.Length..]
            : Path.GetFileName(normalizedSource);
    }

    private static string AppendDirectorySeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
               path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static string TrimPythonExtension(string path)
    {
        return path.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
            ? path[..^3]
            : path;
    }
}
