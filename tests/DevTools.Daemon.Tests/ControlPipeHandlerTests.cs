using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Control;
using DevTools.Daemon.Desktop;
using DevTools.Daemon.Views;
using DevTools.Ipc;
using DevTools.Mcp.Core.Sessions;
using DevTools.Daemon.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevTools.Daemon.Tests;

[Collection(nameof(MewUiApplicationCollection))]
public sealed class ControlPipeHandlerTests(MewUiSession session) : MewUiApplicationTestBase(session)
{
    [Fact]
    public void HandleRequestAsync_Status_ReturnsRunningVersion()
    {
        RunHandler(async handler =>
        {
            var response = await handler.HandleRequestAsync(
                """{"method":"control/status"}""",
                CancellationToken.None);

            Assert.Contains("\"isRunning\":true", response, StringComparison.Ordinal);
            Assert.Contains("\"version\"", response, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void HandleRequestAsync_AuthState_ReturnsAuthFields()
    {
        var auth = DaemonTestDoubles.CreateAuthService(authenticated: true);
        RunHandler(async handler =>
        {
            var response = await handler.HandleRequestAsync(
                """{"method":"control/auth_state"}""",
                CancellationToken.None);

            Assert.Contains("\"isAuthenticated\":true", response, StringComparison.Ordinal);
            Assert.Contains("user@example.com", response, StringComparison.Ordinal);
        }, auth);
    }

    [Fact]
    public void HandleRequestAsync_SignInAndSignOut_ReturnOperationResponses()
    {
        var auth = DaemonTestDoubles.CreateAuthService();
        RunHandler(async handler =>
        {
            var signIn = await handler.HandleRequestAsync(
                """{"method":"control/sign_in"}""",
                CancellationToken.None);
            Assert.Contains("\"success\":true", signIn, StringComparison.Ordinal);

            var signOut = await handler.HandleRequestAsync(
                """{"method":"control/sign_out"}""",
                CancellationToken.None);
            Assert.Contains("\"success\":true", signOut, StringComparison.Ordinal);
            auth.Verify(a => a.SignOutAsync(), Times.Once);
        }, auth);
    }

    [Fact]
    public void HandleRequestAsync_ConnectedHosts_ReturnsCatalogEntries()
    {
        var entry = DaemonTestDoubles.CreateCatalogEntry("Revit", "2025", 4242);
        var broker = DaemonTestDoubles.CreateHostBroker([entry]);
        RunHandler(async handler =>
        {
            var response = await handler.HandleRequestAsync(
                """{"method":"control/connected_hosts"}""",
                CancellationToken.None);

            var hosts = JsonSerializer.Deserialize(response, ControlJsonContext.Default.HostInfoEntryArray);
            Assert.NotNull(hosts);
            Assert.Single(hosts!);
            Assert.Equal(4242, hosts![0].Pid);
        }, broker: broker);
    }

    [Fact]
    public void HandleRequestAsync_OpenDashboard_ShowsMainWindow()
    {
        RunHandler(async handler =>
        {
            var response = await handler.HandleRequestAsync(
                """{"method":"control/open_dashboard"}""",
                CancellationToken.None);
            Assert.Contains("\"success\":true", response, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void HandleRequestAsync_UnknownMethod_ReturnsError()
    {
        RunHandler(async handler =>
        {
            var response = await handler.HandleRequestAsync(
                """{"method":"control/unknown"}""",
                CancellationToken.None);
            Assert.Contains(IpcConstants.Errors.UnknownMethod, response, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void HandleRequestAsync_InvalidJson_ReturnsInvalidRequest()
    {
        RunHandler(async handler =>
        {
            var response = await handler.HandleRequestAsync("{", CancellationToken.None);
            Assert.Contains(IpcConstants.Errors.InvalidRequest, response, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ControlPipeHostedService_RespondsToClientRequest()
    {
        var auth = DaemonTestDoubles.CreateAuthService();
        var broker = DaemonTestDoubles.CreateHostBroker();
        var handler = default(ControlPipeHandler);
        var tray = default(TrayMenu);

        RunOnUi(() =>
        {
            var state = CreateAppState(auth.Object, broker.Object);
            var window = new MainWindow(state);
            tray = new TrayMenu(state, window);
            handler = new ControlPipeHandler(auth.Object, broker.Object, tray);
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var service = new ControlPipeHostedService(
            handler!,
            NullLogger<ControlPipeHostedService>.Instance);
        await service.StartAsync(cts.Token);

        try
        {
            await using var client = new NamedPipeClientStream(
                ".",
                IpcConstants.ControlPipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await client.ConnectAsync(2000, cts.Token);

            var request = Encoding.UTF8.GetBytes("""{"method":"control/status"}""" + "\n");
            await client.WriteAsync(request, cts.Token);
            await client.FlushAsync(cts.Token);

            var buffer = new byte[4096];
            var read = await client.ReadAsync(buffer, cts.Token);
            var response = Encoding.UTF8.GetString(buffer, 0, read);
            Assert.Contains("\"isRunning\":true", response, StringComparison.Ordinal);
        }
        finally
        {
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);
            tray?.Dispose();
        }
    }

    private void RunHandler(
        Func<ControlPipeHandler, Task> body,
        Mock<IAuthService>? auth = null,
        Mock<IHostBroker>? broker = null)
    {
        auth ??= DaemonTestDoubles.CreateAuthService();
        broker ??= DaemonTestDoubles.CreateHostBroker();

        RunOnUi(() =>
        {
            var state = CreateAppState(auth.Object, broker.Object);
            var window = new MainWindow(state);
            using var tray = new TrayMenu(state, window);
            var handler = new ControlPipeHandler(auth.Object, broker.Object, tray);
            body(handler).GetAwaiter().GetResult();
        });
    }

    private static AppState CreateAppState(IAuthService auth, IHostBroker broker)
    {
        var scanner = DaemonTestDoubles.CreatePipeScanner();
        var store = DaemonTestDoubles.CreateUserSettingsStore();
        var tunnel = DaemonTestDoubles.CreateTunnelStatus();
        return new AppState(auth, broker, scanner.Object, store, tunnel.Object);
    }
}
