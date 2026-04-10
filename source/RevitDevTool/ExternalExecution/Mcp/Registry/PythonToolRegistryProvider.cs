using System.Diagnostics;
using System.IO;
using Python.Runtime;
using RevitDevTool.Execution.Providers.Python;
using RevitDevTool.McpParser.Models;
using RevitDevTool.McpParser.Python;

namespace RevitDevTool.ExternalExecution.Mcp.Registry;

public sealed class PythonToolRegistryProvider(PythonInitializer pythonInitializer) : IMcpRegistryProvider
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

        if (pythonInitializer.Provider is null || !pythonInitializer.Provider.IsEnvironmentReady())
        {
            Trace.TraceWarning("[MCP] Python environment is not ready. Skipping Python MCP registry discovery.");
            return McpRegistryCatalog.Empty;
        }

        PreResolveDependencies(ToolsetDirectories);

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

    private void PreResolveDependencies(IReadOnlyList<string> directories)
    {
        foreach (var dir in directories
                     .Where(Directory.Exists)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var entryFile in FindMcpEntryFiles(dir))
            {
                try
                {
                    Trace.TraceInformation($"[MCP] Pre-resolving dependencies for '{entryFile}'...");
                    PythonExecutionStrategy.ResolveDependenciesAsync(pythonInitializer, entryFile).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"[MCP] Dependency pre-resolve failed for '{entryFile}': {ex.Message}");
                }
            }
        }
    }

    private static IEnumerable<string> FindMcpEntryFiles(string directory)
    {
        if (!Directory.Exists(directory))
            return [];

        return Directory.EnumerateFiles(directory, McpPathValidator.PythonToolPattern, SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
    }

    private string? ParseDirectory(string toolsetDirectory)
    {
        if (!pythonInitializer.IsInitialized || pythonInitializer.GlobalScope is null)
            return null;

        using (Py.GIL())
        {
            using var scope = pythonInitializer.GlobalScope.NewScope();
            scope.Set(PythonScopeVars.ToolsetDirectory, new PyString(toolsetDirectory));
            scope.Exec(PythonEmbedded.ToolParserScript);
            return scope.Get(PythonScopeVars.ParserResult).As<string>();
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