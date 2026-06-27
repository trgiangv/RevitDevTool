using System.Diagnostics;

namespace DevTools.Mcp.Registry;

public sealed class DotnetMcpRegistryProvider : IMcpRegistryProvider
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
