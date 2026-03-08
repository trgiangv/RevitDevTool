using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
using RevitDevTool.Execution.Models;
using RevitDevTool.Execution.Providers.Python;
using RevitDevTool.Mcp.Schemas;

namespace RevitDevTool.Mcp.Python;

public sealed class PythonToolsetParser
{
    private const int LogPreviewLength = 400;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyList<McpToolDefinition> ParseDirectory(string toolsetDirectory)
    {
        var parserOutput = RunParserProcess(toolsetDirectory);
        if (parserOutput is null)
            return [];

        var tools = DeserializeTools(parserOutput, toolsetDirectory);
        if (tools is null)
            return [];

        NormalizeToolDefinitions(tools, toolsetDirectory);
        return tools;
    }

    private static string? RunParserProcess(string directory)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var result = Cli.Wrap(PixiEnvironment.PythonExe)
            .WithWorkingDirectory(PixiEnvironment.McpServerDir)
            .WithArguments([PixiEnvironment.FastMcpParserPath, directory])
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdout))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stderr))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync()
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        var output = stdout.ToString().Trim();
        var errorOutput = stderr.ToString().Trim();

        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            Trace.TraceWarning(
                $"[MCP] FastMCP parser failed for '{directory}'. exitCode={result.ExitCode}, stdout='{Preview(output)}', stderr='{Preview(errorOutput)}'");
            return null;
        }

        Trace.TraceInformation(
            $"[MCP] FastMCP parser succeeded for '{directory}'. exitCode={result.ExitCode}, stderr='{Preview(errorOutput)}'");
        return output;
    }

    private static List<McpToolDefinition>? DeserializeTools(string json, string directory)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<McpToolDefinition>>(json, JsonOptions);
            if (items is null)
            {
                Trace.TraceWarning(
                    $"[MCP] FastMCP parser returned null tool list for '{directory}'. stdout='{Preview(json)}'");
            }
            return items;
        }
        catch (JsonException ex)
        {
            Trace.TraceWarning(
                $"[MCP] FastMCP parser JSON deserialize failed for '{directory}': {ex.Message}. stdout='{Preview(json)}'");
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
            var toolsetName = Path.GetFileName(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? directory;

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

    private static string Preview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // ReSharper disable once RedundantSuppressNullableWarningExpression
        var normalized = text!.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= LogPreviewLength
            ? normalized
            : normalized[..LogPreviewLength] + "...";
    }
}
