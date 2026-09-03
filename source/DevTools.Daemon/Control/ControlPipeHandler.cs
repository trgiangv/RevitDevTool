using System.Text.Json;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Views;
using DevTools.Ipc;
using DevTools.Mcp.Core.Sessions;
using DevTools.Mcp.Server.Utils;

namespace DevTools.Daemon.Control;

public sealed class ControlPipeHandler(IAuthService authService, IHostBroker hostBroker, TrayMenu trayMenu)
{
    private const string DefaultVersion = "0.0.0";

    public async Task<string> HandleRequestAsync(string requestLine, CancellationToken ct)
    {
        try
        {
            using var request = JsonDocument.Parse(requestLine);
            var method = request.RootElement.TryGetProperty(IpcPropertyNames.Method, out var methodElement)
                ? methodElement.GetString()
                : null;

            return method switch
            {
                IpcConstants.Methods.Status => JsonSerializer.Serialize(new StatusResponse(
                    true, typeof(ControlPipeHandler).Assembly.GetName().Version?.ToString() ?? DefaultVersion)),
                IpcConstants.Methods.AuthState => JsonSerializer.Serialize(new AuthStateResponse(
                    authService.IsAuthenticated, authService.UserId, authService.Email, authService.DisplayName, authService.AvatarUrl)),
                IpcConstants.Methods.SignIn => await HandleSignInAsync(ct).ConfigureAwait(false),
                IpcConstants.Methods.SignOut => await HandleSignOutAsync().ConfigureAwait(false),
                IpcConstants.Methods.ConnectedHosts => HandleConnectedHosts(),
                IpcConstants.Methods.OpenDashboard => HandleOpenMainWindow(),
                _ => JsonSerializer.Serialize(new ErrorResponse(IpcConstants.Errors.UnknownMethod))
            };
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new ErrorResponse(IpcConstants.Errors.InvalidRequest));
        }
    }

    private string HandleConnectedHosts()
    {
        var hosts = hostBroker.Catalog.List()
            .Select(e => new HostInfoEntry(
                HostAppParsing.ParseHostApp(e.Instance.HostApp)
                ?? HostAppParsing.FromPipeName(e.PipeName),
                e.Instance.VersionNumber,
                e.Instance.ProcessId,
                e.PipeName))
            .ToArray();

        return JsonSerializer.Serialize(hosts);
    }

    private async Task<string> HandleSignInAsync(CancellationToken ct)
    {
        var result = await authService.SignInAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Serialize(new OperationResponse(result.Success, result.Error));
    }

    private async Task<string> HandleSignOutAsync()
    {
        await authService.SignOutAsync().ConfigureAwait(false);
        return JsonSerializer.Serialize(new OperationResponse(true));
    }

    private string HandleOpenMainWindow()
    {
        trayMenu.ShowMainWindow();
        return JsonSerializer.Serialize(new OperationResponse(true));
    }
}
