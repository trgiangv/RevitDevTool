using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Hosting;
using DevTools.Mcp.Schema;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Daemon.Mcp.Tools;

public sealed class ListMachinesTool(IAuthService authService, IOptions<GatewayOptions> gatewayOptions) : McpServerTool
{
    private static class ToolMetadata
    {
        public const string Name = "list_machines";
        public const string Description =
            "List gateway machines after the gateway has selected this daemon. Machine selection uses x-target-machine before MCP initialization; hostId is only a local process ID.";
    }

    private static readonly HttpClient Http = new();

    public override Tool ProtocolTool { get; } = new()
    {
        Name = ToolMetadata.Name,
        Description = ToolMetadata.Description,
        InputSchema = McpSchemaBuilder.EmptyObject()
    };

    public override IReadOnlyList<object> Metadata => [];

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        if (!authService.IsAuthenticated || authService.AccessToken is not { } token)
            return ToolHelpers.ErrorResult("Not authenticated. Sign in to the DevTools daemon first.");

        try
        {
            var url = $"{gatewayOptions.Value.HttpBaseUrl}{DaemonConstants.RoutePaths.Machines}";
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
