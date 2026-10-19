using DevTools.Daemon.Mcp.Prompts;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Mcp.Tools;
using DevTools.FileMetadata.Core;
using DevTools.Hosting;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Hosting;

/// <summary>
/// Owns the external MCP tool/prompt collections for the daemon.
/// Host capabilities are never projected here. The model reaches them through code_mode.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class McpEngine
{
    public McpServerPrimitiveCollection<McpServerTool> ToolCollection { get; }
    public McpServerPrimitiveCollection<McpServerPrompt> PromptCollection { get; }
    public IReadOnlyList<McpServerTool> LocalTools { get; }

    public McpEngine(
        IHostConnections sessions,
        IMcpPipeScanner pipeScanner,
        IHostLaunchService launchService,
        IFileReaderCatalog fileInfoCatalog)
    {
        ToolCollection = [];
        PromptCollection = [];

        LocalTools = CreateLocalTools(sessions, pipeScanner, launchService, fileInfoCatalog);
        foreach (var tool in LocalTools)
            ToolCollection.TryAdd(tool);

        PromptCollection.TryAdd(RevitCodePrompt.Create());
        PromptCollection.TryAdd(AcadCodePrompt.Create());
    }

    private static McpServerTool[] CreateLocalTools(
        IHostConnections sessions,
        IMcpPipeScanner pipeScanner,
        IHostLaunchService launchService,
        IFileReaderCatalog fileInfoCatalog) =>
    [
        ListProcessesTool.Create(sessions, pipeScanner),
        LaunchHostTool.Create(sessions, launchService),
        ReadFileInfoTool.Create(fileInfoCatalog),
        CodeModeTool.Create(sessions)
    ];
}
