using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Tests;

internal static class TaskModeFixture
{
    [McpMeta(McpTaskExecutionMeta.MetaKey, McpTaskExecutionMeta.Mode.Optional)]
    public static string OptionalHandler() => "ok";

    public static McpServerTool CreateOptionalTool(string name) =>
        McpServerTool.Create(
            OptionalHandler,
            new McpServerToolCreateOptions { Name = name });
}
