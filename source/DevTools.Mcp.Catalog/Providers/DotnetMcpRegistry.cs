using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Catalog;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Discovery;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Mcp.Providers;

public sealed class DotnetMcpRegistry(
    McpAssemblyParser parser,
    ILogger<DotnetMcpRegistry> logger) : IRegistryProvider
{
    public string Name => "dotnet-mcp";
    public ExecutionMode SourceKind => ExecutionMode.Dotnet;
    private IReadOnlyList<string> AssemblyPaths { get; set; } = [];

    public void ConfigurePaths(IReadOnlyList<string> paths)
    {
        AssemblyPaths = paths;
    }

    public RegistryCatalog LoadCatalog()
    {
        var catalog = RegistryCatalog.Empty;
        foreach (var assemblyPath in AssemblyPaths)
        {
            try
            {
                catalog = catalog.Merge(parser.ParseCatalogFromAssembly(assemblyPath));
            }
            catch (Exception ex)
            {
                logger.ZLogWarning($"Failed to parse .NET MCP tools from '{assemblyPath}': {ex.Message}");
            }
        }

        return catalog;
    }
}
