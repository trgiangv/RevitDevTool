using System.Diagnostics;
using RevitDevTool.Contracts;
using RevitDevTool.Mcp.Interfaces;
namespace RevitDevTool.Mcp.Dotnet;

public sealed class DotnetMcpToolRegistryProvider : IMcpToolRegistryProvider
{
    public string Name => "dotnet-mcp";
    public ExecutionMode SourceKind => ExecutionMode.Assembly;
    private IReadOnlyList<string> AssemblyPaths { get; set; } = [];

    public void ConfigurePaths(IReadOnlyList<string> paths)
    {
        AssemblyPaths = paths;
    }

    public IReadOnlyList<McpToolDefinition> LoadTools()
    {
        var tools = new List<McpToolDefinition>();
        foreach (var assemblyPath in AssemblyPaths)
        {
            try
            {
                tools.AddRange(DotnetMcpAssemblyParser.ParseToolsFromAssembly(assemblyPath));
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[MCP] Failed to parse .NET MCP tools from '{assemblyPath}': {ex.Message}");
            }
        }

        return tools;
    }
}
