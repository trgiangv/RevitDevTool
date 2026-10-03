using System.IO;
using DevTools.Execution.Providers.Python;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;
using Microsoft.Extensions.Logging;
using Python.Runtime;
using ZLogger;
namespace DevTools.Execution.External.Mcp.Registry;

public sealed class PythonMcpRegistryProvider(
    PythonInitializer pythonInitializer,
    McpPythonParser parser,
    ILogger<PythonMcpRegistryProvider> logger) : IRegistryProvider
{
    public string Name => "python-mcp";
    public ExecutionMode SourceKind => ExecutionMode.Python;

    private IReadOnlyList<string> Directories { get; set; } = [];

    public void ConfigurePaths(IReadOnlyList<string> paths)
    {
        Directories = paths;
    }

    public RegistryCatalog LoadCatalog()
    {
        if (Directories.Count == 0)
            return RegistryCatalog.Empty;

        if (!pythonInitializer.IsInitialized)
        {
            logger.ZLogWarning($"Python environment is not ready. Skipping Python MCP registry discovery");
            return RegistryCatalog.Empty;
        }

        PreResolveDependencies(Directories);

        var all = RegistryCatalog.Empty;

        foreach (var dir in Directories
                     .Where(Directory.Exists)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                all = all.Merge(parser.ParseCatalogFromDirectory(dir, ParseDirectory));
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"Failed to parse Python directory '{dir}': {ex.Message}\n{ex.StackTrace}");
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
                    logger.ZLogInformation($"Pre-resolving dependencies for '{entryFile}'...");
                    PythonExecutionStrategy.ResolveDependenciesAsync(pythonInitializer, entryFile).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    logger.ZLogWarning($"Dependency pre-resolve failed for '{entryFile}': {ex.Message}");
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

    private string? ParseDirectory(string directory)
    {
        if (!pythonInitializer.IsInitialized)
            return null;

        var anchorFile = Path.Combine(directory, "__mcp_registry__.py");
        return PythonExecutor.Execute(
            pythonInitializer,
            anchorFile,
            directory,
            scope =>
            {
            scope.Set(PythonInstances.ToolsetDirectory, new PyString(directory));
            scope.Exec(PythonEmbedded.ToolParserScript);
            return scope.Get(PythonInstances.ParserResult).As<string>();
            });
    }

    private void LogMissingDirectories()
    {
        foreach (var missingDir in Directories
                     .Where(path => !Directory.Exists(path))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            logger.ZLogWarning($"Python directory not found: {missingDir}");
        }
    }
}