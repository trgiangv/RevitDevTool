using ModelContextProtocol.Extensions.Tasks;

namespace McpToolsetDemo;

/// <summary>
/// Bridges SDK <see cref="McpTaskExecutionMode"/> to per-tool <c>[McpMeta]</c> declarations.
/// </summary>
/// <remarks>
/// <see cref="MetaKey"/> is the only host convention. Mode strings use
/// <c>nameof(McpTaskExecutionMode)</c> — aligned with <c>DevTools.Mcp.Core.McpTaskExecutionMeta</c>.
/// Requires package <c>ModelContextProtocol.Extensions.Tasks</c>.
/// </remarks>
public static class McpTaskExecutionMeta
{
    public const string MetaKey = "tasks.executionMode";

    public static class Mode
    {
        public const string Synchronous = nameof(McpTaskExecutionMode.Synchronous);
        public const string Optional = nameof(McpTaskExecutionMode.Optional);
        public const string Required = nameof(McpTaskExecutionMode.Required);
    }
}
