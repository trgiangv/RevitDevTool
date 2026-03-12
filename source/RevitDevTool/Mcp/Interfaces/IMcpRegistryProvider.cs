using RevitDevTool.Contracts;
using RevitDevTool.Mcp.Parser.Models;

namespace RevitDevTool.Mcp.Interfaces;

public interface IMcpRegistryProvider
{
    string Name { get; }
    ExecutionMode SourceKind { get; }
    void ConfigurePaths(IReadOnlyList<string> paths);
    McpRegistryCatalog LoadCatalog();
}