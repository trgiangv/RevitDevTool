using DevTools.Daemon.Mcp.Prompts;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Mcp.Tools;
using DevTools.FileMetadata.Core;
using DevTools.Hosting;
using DevTools.Daemon.Mcp.Contracts;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Hosting;

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
        IProcessSessions sessions,
        IMcpPipeScanner pipeScanner,
        IHostLaunchService launchService,
        IMachineLister machineLister,
        IFileReaderCatalog fileInfoCatalog)
    {
        ToolCollection = [];
        PromptCollection = [];

        LocalTools = CreateLocalTools(sessions, pipeScanner, launchService, machineLister, fileInfoCatalog);
        foreach (var tool in LocalTools)
            ToolCollection.TryAdd(tool);

        PromptCollection.TryAdd(RevitCodePrompt.Create());
        PromptCollection.TryAdd(AcadCodePrompt.Create());
    }

    private static McpServerTool[] CreateLocalTools(
        IProcessSessions sessions,
        IMcpPipeScanner pipeScanner,
        IHostLaunchService launchService,
        IMachineLister machineLister,
        IFileReaderCatalog fileInfoCatalog) =>
    [
        ListMachinesTool.Create(machineLister),
        ListProcessesTool.Create(sessions, pipeScanner),
        LaunchHostTool.Create(sessions, launchService),
        ReadFileInfoTool.Create(fileInfoCatalog),
        SearchTool.Create(sessions),
        InvokeTool.Create(sessions)
    ];
}
