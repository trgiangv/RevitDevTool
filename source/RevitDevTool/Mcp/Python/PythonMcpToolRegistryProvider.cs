using System.Diagnostics;
using System.IO;
using RevitDevTool.Execution.Models;
using RevitDevTool.Execution.Providers.Python;
using RevitDevTool.Mcp.Interfaces;
using RevitDevTool.Mcp.Schemas;
namespace RevitDevTool.Mcp.Python;

public sealed class PythonMcpToolRegistryProvider : IMcpToolRegistryProvider
{
    public string Name => "python-mcp";
    public ExecutionMode SourceKind => ExecutionMode.Python;

    private IReadOnlyList<string> ToolsetDirectories { get; set; } = [];

    public void ConfigurePaths(IReadOnlyList<string> paths)
    {
        ToolsetDirectories = paths;
    }

    public IReadOnlyList<McpToolDefinition> LoadTools()
    {
        if (ToolsetDirectories.Count == 0)
            return [];

        PythonBootstrap.EnsureEnvironmentReadyAsync()
            .ConfigureAwait(false).GetAwaiter().GetResult();

        Trace.TraceInformation(
            $"[MCP] Python registry load starting. toolsetDirs={ToolsetDirectories.Count}, pythonExe='{PixiEnvironment.PythonExe}', parser='{PixiEnvironment.FastMcpParserPath}'");

        var all = new List<McpToolDefinition>();

        foreach (var dir in ToolsetDirectories
                     .Where(Directory.Exists)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                all.AddRange(PythonToolsetParser.ParseDirectory(dir));
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
