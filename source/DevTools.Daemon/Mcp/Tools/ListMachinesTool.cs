using System.Net.Http;
using System.Net.Http.Headers;
using System.ComponentModel;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

[McpServerToolType]
public sealed class ListMachinesTool(IAuthService authService, IOptions<GatewayOptions> gatewayOptions)
{
    private static readonly HttpClient Http = new();

    [McpServerTool(Name = "list_machines")]
    [Description("List gateway machines after the gateway has selected this daemon. Machine selection uses x-target-machine before MCP initialization; hostId is only a local process ID.")]
    public async Task<CallToolResult> ListAsync(
        CancellationToken cancellationToken = default)
    {
        if (!authService.IsAuthenticated || authService.AccessToken is not { } token)
            return ToolHelpers.ErrorResult("Not authenticated. Sign in to the DevTools daemon first.");

        try
        {
            var url = $"{gatewayOptions.Value.HttpBaseUrl}{GatewayRouteConstants.Machines}";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue(GatewayTunnelClient.BearerScheme, token);

            using var response = await Http.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return ToolHelpers.ErrorResult($"Gateway returned {(int)response.StatusCode}: {body}");

            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = body }]
            };
        }
        catch (Exception ex)
        {
            return ToolHelpers.ErrorResult($"Failed to list machines: {ex.Message}");
        }
    }
}
