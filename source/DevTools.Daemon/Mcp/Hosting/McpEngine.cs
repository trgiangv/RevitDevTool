using DevTools.Daemon.Auth;
using DevTools.Daemon.Gateway;
using DevTools.Daemon.Mcp.Prompts;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Mcp.Tools;
using DevTools.FileMetadata.Core;
using DevTools.Hosting;
using Microsoft.Extensions.Options;
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
        IProcessSessions sessions,
        IMcpPipeScanner pipeScanner,
        IHostLaunchService launchService,
        IAuthService authService,
        IOptions<GatewayOptions> gatewayOptions,
        IFileReaderCatalog fileInfoCatalog)
    {
        ToolCollection = [];
        PromptCollection = [];

        LocalTools = CreateLocalTools(sessions, pipeScanner, launchService, authService, gatewayOptions, fileInfoCatalog);
        foreach (var tool in LocalTools)
            ToolCollection.TryAdd(tool);

        PromptCollection.TryAdd(RevitCodePrompt.Create());
        PromptCollection.TryAdd(AcadCodePrompt.Create());
    }

    private static McpServerTool[] CreateLocalTools(
        IProcessSessions sessions,
        IMcpPipeScanner pipeScanner,
        IHostLaunchService launchService,
        IAuthService authService,
        IOptions<GatewayOptions> gatewayOptions,
        IFileReaderCatalog fileInfoCatalog) =>
    [
        ListMachinesTool.Create(authService, gatewayOptions),
        ListProcessesTool.Create(sessions, pipeScanner),
        LaunchHostTool.Create(sessions, launchService),
        ReadFileInfoTool.Create(fileInfoCatalog),
        CodeModeTool.Create(sessions)
    ];
}
