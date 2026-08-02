using DevTools.FileMetadata.Core;
using DevTools.Mcp.Client;
using DevTools.Mcp.Core;
using DevTools.Mcp.Server.Contracts;
using DevTools.Mcp.Server.Prompts;
using DevTools.Mcp.Server.Tools;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Hosting;

/// <summary>
/// Owns the external MCP tool/prompt collections for the daemon.
/// Host capabilities are never projected here — only via search_dynamic / invoke_dynamic.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class McpEngine
{
    public McpServerPrimitiveCollection<McpServerTool> ToolCollection { get; }
    public McpServerPrimitiveCollection<McpServerPrompt> PromptCollection { get; }
    public IReadOnlyList<McpServerTool> LocalTools { get; }

    public McpEngine(
        IHostBroker hostBroker,
        IMcpPipeScanner pipeScanner,
        IHostLaunchService launchService,
        IMachineLister machineLister,
        IFileReaderCatalog fileInfoCatalog)
    {
        ToolCollection = [];
        PromptCollection = [];

        LocalTools = CreateLocalTools(hostBroker, pipeScanner, launchService, machineLister, fileInfoCatalog);
        foreach (var tool in LocalTools)
            ToolCollection.TryAdd(tool);

        PromptCollection.TryAdd(RevitCodePrompt.Create());
        PromptCollection.TryAdd(AcadCodePrompt.Create());
    }

    private static McpServerTool[] CreateLocalTools(
        IHostBroker hostBroker,
        IMcpPipeScanner pipeScanner,
        IHostLaunchService launchService,
        IMachineLister machineLister,
        IFileReaderCatalog fileInfoCatalog) =>
    [
        ListMachinesTool.Create(machineLister),
        ListHostInstancesTool.Create(hostBroker, pipeScanner),
        LaunchHostTool.Create(hostBroker, launchService),
        ReadFileInfoTool.Create(fileInfoCatalog),
        SearchDynamicTool.Create(hostBroker),
        InvokeDynamicTool.Create(hostBroker)
    ];
}
