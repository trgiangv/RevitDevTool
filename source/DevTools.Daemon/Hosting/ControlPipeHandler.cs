using System.Text.Json;
using System.Windows;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Contracts;
using DevTools.Daemon.Mcp;
using DevTools.Daemon.Mcp.Tools;
using DevTools.Daemon.Tray;
using H.NotifyIcon;

namespace DevTools.Daemon.Hosting;

/// <summary>
/// Handles individual control pipe requests. Stateless — one method per JSON-RPC method.
/// </summary>
public sealed class ControlPipeHandler(IAuthService authService, McpEngine mcpEngine)
{
    private const string DefaultVersion = "0.0.0";

    public async Task<string> HandleRequestAsync(string requestLine, CancellationToken ct)
    {
        try
        {
            using var request = JsonDocument.Parse(requestLine);
            var method = request.RootElement.TryGetProperty("method", out var methodElement)
                ? methodElement.GetString()
                : null;

            return method switch
            {
                DaemonConstants.Methods.Status => JsonSerializer.Serialize(new StatusResponse(
                    true, typeof(ControlPipeHandler).Assembly.GetName().Version?.ToString() ?? DefaultVersion)),
                DaemonConstants.Methods.AuthState => JsonSerializer.Serialize(new AuthStateResponse(
                    authService.IsAuthenticated, authService.UserId, authService.Email, authService.DisplayName, authService.AvatarUrl)),
                DaemonConstants.Methods.SignIn => await HandleSignInAsync(ct).ConfigureAwait(false),
                DaemonConstants.Methods.SignOut => await HandleSignOutAsync().ConfigureAwait(false),
                DaemonConstants.Methods.ConnectedHosts => HandleConnectedHosts(),
                DaemonConstants.Methods.OpenDashboard => HandleOpenDashboard(),
                _ => JsonSerializer.Serialize(new ErrorResponse(DaemonConstants.Errors.UnknownMethod))
            };
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(new ErrorResponse(DaemonConstants.Errors.InvalidRequest));
        }
    }

    private string HandleConnectedHosts()
    {
        var instanceManager = mcpEngine.InstanceManager;
        var hosts = instanceManager.Sessions
            .Select(session => new HostInfoEntry(
                HostAppExtensions.ParseHostApp(session.Instance.HostApp) ?? HostAppExtensions.FromPipeName(session.Instance.PipeName),
                session.Instance.VersionNumber,
                session.Instance.ProcessId,
                session.Instance.PipeName))
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

    private static string HandleOpenDashboard()
    {
        var app = Application.Current;
        if (app is null)
            return JsonSerializer.Serialize(new OperationResponse(false));

        app.Dispatcher.Invoke(() =>
        {
            if (app.FindResource(DaemonConstants.TrayIconResourceKey) is TaskbarIcon { DataContext: TrayViewModel vm })
                vm.OpenDashboardCommand.Execute(null);
        });

        return JsonSerializer.Serialize(new OperationResponse(true));
    }
}
