namespace DevTools.Mcp.Core;

public interface IMcpCatalogLoader
{
    McpRegistryCatalog LoadCatalog(
        IReadOnlyCollection<string> dotnetPaths,
        IReadOnlyCollection<string> pythonToolsetPaths);
}
