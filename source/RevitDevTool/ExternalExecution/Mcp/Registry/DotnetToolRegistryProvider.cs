using System.Diagnostics;
using DevTools.McpParser.Dotnet;
using DevTools.McpParser.Models;

namespace RevitDevTool.ExternalExecution.Mcp.Registry;

public sealed class DotnetToolRegistryProvider : IMcpRegistryProvider
{
    public string Name => "dotnet-mcp";
    public ExecutionMode SourceKind => ExecutionMode.Assembly;
    private IReadOnlyList<string> AssemblyPaths { get; set; } = [];

    public void ConfigurePaths(IReadOnlyList<string> paths)
    {
        AssemblyPaths = paths;
    }

    public McpRegistryCatalog LoadCatalog()
    {
        var catalog = McpRegistryCatalog.Empty;
        foreach (var assemblyPath in AssemblyPaths)
        {
            try
            {
                catalog = catalog.Merge(DotnetMcpAssemblyParser.ParseCatalogFromAssembly(assemblyPath));
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[MCP] Failed to parse .NET MCP tools from '{assemblyPath}': {ex.Message}");
            }
        }

        return catalog;
    }
}