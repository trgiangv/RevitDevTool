namespace DevTools.Mcp.Registry;

public interface IMcpRegistryProvider
{
    string Name { get; }
    int Priority { get; }
    ExecutionMode SourceKind { get; }
    void ConfigurePaths(IReadOnlyList<string> paths);
    McpRegistryCatalog LoadCatalog();
}
