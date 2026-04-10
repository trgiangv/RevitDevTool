using RevitDevTool.McpParser.Models;

namespace RevitDevTool.ExternalExecution.Mcp.Registry;

public interface IMcpRegistryProvider
{
    string Name { get; }
    ExecutionMode SourceKind { get; }
    void ConfigurePaths(IReadOnlyList<string> paths);
    McpRegistryCatalog LoadCatalog();
}