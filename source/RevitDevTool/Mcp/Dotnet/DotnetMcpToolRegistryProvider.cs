using System.Diagnostics;
using RevitDevTool.Mcp.Interfaces;
using RevitDevTool.McpParser.Dotnet;
using RevitDevTool.McpParser.Models;
namespace RevitDevTool.Mcp.Dotnet;

public sealed class DotnetMcpToolRegistryProvider : IMcpRegistryProvider
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
