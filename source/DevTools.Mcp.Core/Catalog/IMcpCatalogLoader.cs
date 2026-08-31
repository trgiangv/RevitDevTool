using DevTools.Mcp.Core.Models;
namespace DevTools.Mcp.Core.Catalog;

public interface IMcpCatalogLoader
{
    McpRegistryCatalog LoadCatalog(
        IReadOnlyCollection<string> dotnetPaths,
        IReadOnlyCollection<string> pythonToolsetPaths);
}
