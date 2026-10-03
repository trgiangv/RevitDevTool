using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Models;
namespace DevTools.Mcp.Core.Catalog;

public interface IRegistryProvider
{
    string Name { get; }
    ExecutionMode SourceKind { get; }
    void ConfigurePaths(IReadOnlyList<string> paths);
    RegistryCatalog LoadCatalog();
}
