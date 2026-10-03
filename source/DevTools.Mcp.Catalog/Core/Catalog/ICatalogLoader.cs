using DevTools.Mcp.Core.Models;
namespace DevTools.Mcp.Core.Catalog;

public interface ICatalogLoader
{
    RegistryCatalog LoadCatalog(
        IReadOnlyCollection<string> dotnetPaths,
        IReadOnlyCollection<string> pythonPaths);
}
