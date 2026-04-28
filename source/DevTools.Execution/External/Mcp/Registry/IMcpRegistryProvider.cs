using DevTools.McpParser;
using DevTools.McpParser.Models;

namespace DevTools.Execution.External.Mcp.Registry;

public interface IMcpRegistryProvider
{
    string Name { get; }
    ExecutionMode SourceKind { get; }
    void ConfigurePaths(IReadOnlyList<string> paths);
    McpRegistryCatalog LoadCatalog();
}