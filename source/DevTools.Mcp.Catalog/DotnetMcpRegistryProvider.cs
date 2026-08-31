using DevTools.Execution.Abstractions;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Mcp.Catalog;

public sealed class DotnetMcpRegistryProvider(
    McpAssemblyParser assemblyParser,
    ILogger<DotnetMcpRegistryProvider> logger) : IMcpRegistryProvider
{
    public string Name => "dotnet-mcp";
    public ExecutionMode SourceKind => ExecutionMode.Dotnet;
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
                catalog = catalog.Merge(assemblyParser.ParseCatalogFromAssembly(assemblyPath));
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"Failed to parse .NET MCP tools from '{assemblyPath}': {ex.Message}");
            }
        }

        return catalog;
    }
}
