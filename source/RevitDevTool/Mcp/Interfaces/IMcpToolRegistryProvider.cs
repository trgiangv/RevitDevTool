using RevitDevTool.Execution.Models;
using RevitDevTool.Mcp.Schemas;
namespace RevitDevTool.Mcp.Interfaces;

public interface IMcpToolRegistryProvider
{
    string Name { get; }
    ExecutionMode SourceKind { get; }
    void ConfigurePaths(IReadOnlyList<string> paths);
    IReadOnlyList<McpToolDefinition> LoadTools();
}
