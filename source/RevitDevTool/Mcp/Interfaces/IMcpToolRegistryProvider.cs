using RevitDevTool.Contracts;
namespace RevitDevTool.Mcp.Interfaces;

public interface IMcpToolRegistryProvider
{
    string Name { get; }
    ExecutionMode SourceKind { get; }
    void ConfigurePaths(IReadOnlyList<string> paths);
    IReadOnlyList<McpToolDefinition> LoadTools();
}
