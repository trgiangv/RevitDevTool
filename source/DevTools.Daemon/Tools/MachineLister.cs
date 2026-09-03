using System.ComponentModel;
using System.Net.Http.Headers;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Gateway;
using DevTools.Ipc;
using DevTools.Mcp.Core.Utils;
using DevTools.Mcp.Server.Contracts;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;

namespace DevTools.Daemon.Tools;

public sealed class MachineLister(IAuthService authService, IOptions<GatewayOptions> gatewayOptions) : IMachineLister
{
    private static readonly HttpClient Http = new();

    [Description("List all connected machines for this user.")]
    public async Task<CallToolResult> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!authService.IsAuthenticated || authService.AccessToken is not { } token)
            return ToolHelpers.ErrorResult("Not authenticated. Sign in to the DevTools daemon first.");

        try
        {
            var url = $"{gatewayOptions.Value.HttpBaseUrl}{IpcConstants.RoutePaths.Machines}";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await Http.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return !response.IsSuccessStatusCode ? ToolHelpers.ErrorResult($"Gateway returned {(int)response.StatusCode}: {body}") : ToolHelpers.Result(body);
        }
        catch (Exception ex)
        {
            return ToolHelpers.ErrorResult($"Failed to list machines: {ex.Message}");
        }
    }
}
