using DevTools.McpParser.Models;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Mcp.Catalog;

public sealed record DynamicToolRegistration(Tool Tool, InstanceInfo Instance, string PipeName);

public enum DynamicToolResolutionState
{
    Found,
    NotFound,
    Ambiguous
}

public sealed record DynamicToolResolution(
    DynamicToolResolutionState State,
    DynamicToolRegistration? Registration,
    IReadOnlyList<DynamicToolRegistration> Candidates);
