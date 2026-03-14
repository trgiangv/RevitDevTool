using System.Diagnostics;
using System.IO;
using Python.Runtime;
using RevitDevTool.Execution.Providers.Python;
using RevitDevTool.Mcp.Interfaces;
using RevitDevTool.McpParser.Models;
using RevitDevTool.McpParser.Python;
namespace RevitDevTool.Mcp.Python;

public sealed class PythonMcpToolRegistryProvider : IMcpRegistryProvider
{
    public string Name => "python-mcp";
    public ExecutionMode SourceKind => ExecutionMode.Python;

    private IReadOnlyList<string> ToolsetDirectories { get; set; } = [];

    public void ConfigurePaths(IReadOnlyList<string> paths)
    {
        ToolsetDirectories = paths;
    }

    public McpRegistryCatalog LoadCatalog()
    {
        if (ToolsetDirectories.Count == 0)
            return McpRegistryCatalog.Empty;

        if (!PythonEnvironment.IsEnvironmentReady())
        {
            Trace.TraceWarning("[MCP] Python environment is not ready. Skipping Python MCP registry discovery.");
            return McpRegistryCatalog.Empty;
        }

        var all = McpRegistryCatalog.Empty;

        foreach (var dir in ToolsetDirectories
                     .Where(Directory.Exists)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                all = all.Merge(PythonToolsetParser.ParseDirectoryCatalog(dir, ParseDirectory));
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[MCP] Failed to parse toolset '{dir}': {ex.Message}\n{ex.StackTrace}");
            }
        }

        LogMissingDirectories();
        return all;
    }

    private static string? ParseDirectory(string toolsetDirectory)
    {
        if (!PythonInitializer.IsInitialized || PythonInitializer.GlobalScope is null)
            return null;

        using (Py.GIL())
        {
            using var scope = PythonInitializer.GlobalScope.NewScope();
            scope.Set("__toolset_directory__", new PyString(toolsetDirectory));
            scope.Exec(PythonEmbedded.ToolParserScript);
            return scope.Get("__parser_result__").As<string>();
        }
    }

    private void LogMissingDirectories()
    {
        foreach (var missingDir in ToolsetDirectories
                     .Where(path => !Directory.Exists(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            Trace.TraceWarning($"[MCP] Toolset directory not found: {missingDir}");
        }
    }
}
