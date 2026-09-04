using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Composition;
using DevTools.Daemon.Control;
using DevTools.Daemon.Desktop;
using DevTools.Daemon.Gateway;
using DevTools.Daemon.Tests.Support;
using DevTools.Daemon.Views;
using DevTools.Utilities;
using Duende.IdentityModel.OidcClient.Browser;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DevTools.Daemon.Tests;

public sealed class ExtendedCoverageTests
{
    [Fact]
    public async Task AuthBrowser_CallbackSuccess_ReturnsResponseUrl()
    {
        var port = GetFreePort();
        var callback = $"http://127.0.0.1:{port}/callback";
        var browser = new AuthBrowser(new AuthOptions { LoopbackPort = port });
        var invoke = Task.Run(() => browser.InvokeAsync(
            new BrowserOptions("about:blank", callback),
            TestContext.Current.CancellationToken));

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var attempt = 0; attempt < 80; attempt++)
        {
            try
            {
                await client.GetAsync($"{callback}?code=abc", TestContext.Current.CancellationToken);
                break;
            }
            catch
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }

        var result = await invoke.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(BrowserResultType.Success, result.ResultType);
        Assert.Contains("code=abc", result.Response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthBrowser_CallbackDenied_ReturnsDeniedResponse()
    {
        var port = GetFreePort();
        var callback = $"http://127.0.0.1:{port}/callback";
        var browser = new AuthBrowser(new AuthOptions { LoopbackPort = port });
        var invoke = Task.Run(() => browser.InvokeAsync(
            new BrowserOptions("about:blank", callback),
            TestContext.Current.CancellationToken));

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var attempt = 0; attempt < 80; attempt++)
        {
            try
            {
                await client.GetAsync($"{callback}?error=access_denied", TestContext.Current.CancellationToken);
                break;
            }
            catch
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }

        var result = await invoke.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(BrowserResultType.Success, result.ResultType);
        Assert.Contains("error=access_denied", result.Response, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthService_WithStoredToken_RefreshAndSignOut()
    {
        var tokenPath = Path.Combine(AppUtils.GetApplicationDataPath(), "auth.dat");
        var backup = File.Exists(tokenPath) ? File.ReadAllBytes(tokenPath) : null;
        try
        {
            var token = new TokenData
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds(),
                UserId = "user",
                Email = "user@example.com",
            };
            var json = JsonSerializer.Serialize(token, ControlJsonContext.Default.TokenData);
            File.WriteAllBytes(
                tokenPath,
                ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser));

            using var service = new AuthService(
                Options.Create(new AuthOptions { Issuer = "https://example.com", ClientId = "client" }),
                NullLogger<AuthService>.Instance);

            Assert.True(service.IsAuthenticated);
            Assert.Equal("user@example.com", service.Email);
            Assert.False(await service.RefreshAsync());
            await service.SignOutAsync();
            Assert.False(service.IsAuthenticated);
        }
        finally
        {
            if (backup is null)
                File.Delete(tokenPath);
            else
                File.WriteAllBytes(tokenPath, backup);
        }
    }

    [Fact]
    public async Task Program_Main_StdioArg_ReturnsZeroWhenStdinCloses()
    {
        var originalIn = Console.In;
        try
        {
            var input = new MemoryStream();
            Console.SetIn(new StreamReader(input));
            var task = Task.Run(() => Program.Main(["--stdio"]));
            input.Close();
            Assert.Equal(0, await task);
        }
        finally
        {
            Console.SetIn(originalIn);
        }
    }

    [Fact]
    public async Task GatewayTunnelClient_ConnectsAndReconnectsUntilCancelled()
    {
        using var host = ServerHostBuilder.CreateStdioHostForTests();
        var engine = host.Services.GetRequiredService<DevTools.Mcp.Server.Hosting.McpEngine>();
        var scanner = DaemonTestDoubles.CreatePipeScanner();
        var options = DevTools.Mcp.Server.Hosting.McpServerFactory.CreateOptions(
            engine.ToolCollection, engine.PromptCollection, host.Services);

        var client = new GatewayTunnelClient(
            new Uri("ws://127.0.0.1:9/tunnel"),
            () => Task.FromResult<string?>("token"),
            options,
            scanner.Object,
            NullLoggerFactory.Instance,
            host.Services,
            NullLogger<GatewayTunnelClient>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.RunAsync(cts.Token);
        await client.DisposeAsync();
    }

    [Collection(nameof(MewUiApplicationCollection))]
    public sealed class DesktopUiCoverage(MewUiSession session) : MewUiApplicationTestBase(session)
    {
        [Fact]
        public void MainWindow_TabsAndViews_BuildContent()
        {
            RunOnUi(() =>
            {
                var auth = DaemonTestDoubles.CreateAuthService(authenticated: true);
                auth.Setup(a => a.AvatarUrl).Returns("https://example.com/avatar.png");
                var state = CreateAppState(auth.Object);
                using var window = new MainWindow(state);
                window.Show();

                state.SelectedTabIndex.Value = 0;
                state.SelectedTabIndex.Value = 1;
                state.SelectedTabIndex.Value = 2;
                state.Preferences.ReloadAutoStart();

                BuildView(new OverviewView(state));
                BuildView(new HostsView(state.Hosts));
                BuildView(new SettingsView(state.Preferences, state.Version));
            });
        }

        private static void BuildView(UserControl view)
        {
            var onBuild = typeof(UserControl).GetMethod("OnBuild", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(onBuild);
            _ = onBuild!.Invoke(view, null);
        }

        private static AppState CreateAppState(IAuthService auth)
        {
            var broker = DaemonTestDoubles.CreateHostBroker().Object;
            var scanner = DaemonTestDoubles.CreatePipeScanner().Object;
            var store = DaemonTestDoubles.CreateUserSettingsStore();
            var tunnel = DaemonTestDoubles.CreateTunnelStatus(TunnelStatus.Connected).Object;
            return new AppState(auth, broker, scanner, store, tunnel);
        }
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
