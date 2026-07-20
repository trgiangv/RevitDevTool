using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Mcp.Registry;

public sealed class DotnetMcpRegistryProvider(
    DotnetMcpAssemblyParser assemblyParser,
    ILogger<DotnetMcpRegistryProvider> logger) : IMcpRegistryProvider
{
    public string Name => "dotnet-mcp";
    public int Priority => 100;
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
