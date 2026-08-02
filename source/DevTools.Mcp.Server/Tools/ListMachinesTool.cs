using System.ComponentModel;
using DevTools.Mcp.Server.Contracts;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Tools;

public sealed class ListMachinesTool(IMachineLister machineLister)
{
    public static McpServerTool Create(IMachineLister machineLister)
    {
        var handler = new ListMachinesTool(machineLister);
        return McpServerTool.Create(
            handler.ListAsync,
            new McpServerToolCreateOptions
            {
                Name = "list_machines",
                Description =
                    "List all connected machines for this user. Returns device names and running host apps per machine.",
                ReadOnly = true,
                Destructive = false,
                OpenWorld = true
            });
    }

    [Description("List all connected machines for this user.")]
    public Task<CallToolResult> ListAsync(CancellationToken cancellationToken = default) =>
        machineLister.ListAsync(cancellationToken);
}
