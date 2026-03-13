using System.Diagnostics;
using System.IO;
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
                all = all.Merge(PythonToolsetParser.ParseDirectoryCatalog(dir, PythonEnvironment.PythonExe, PythonEmbedded.ToolParserScriptPath));
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[MCP] Failed to parse toolset '{dir}': {ex.Message}\n{ex.StackTrace}");
            }
        }

        LogMissingDirectories();
        return all;
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
