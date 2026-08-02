using DevTools.Execution.Abstractions;

namespace DevTools.Mcp.Core;

public interface IMcpRegistryProvider
{
    string Name { get; }
    ExecutionMode SourceKind { get; }
    void ConfigurePaths(IReadOnlyList<string> paths);
    McpRegistryCatalog LoadCatalog();
}
