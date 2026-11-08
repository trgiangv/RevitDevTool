using DevTools.Daemon.Auth;
using DevTools.Daemon.Control;
using DevTools.Daemon.Desktop.ViewModels;
using DevTools.Daemon.Desktop.Views;
using DevTools.Daemon.Tests.Support;
using DevTools.Daemon.Tunnel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace DevTools.Daemon.Tests;

[TestClass]
public sealed class DaemonHostIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void CreateDesktop_RegistersDesktopServices()
    {
        WpfApplicationTestBase.RunShared(() =>
        {
            using var host = AppHostBuilder.CreateDesktop();
            Assert.IsNotNull(host.Services.GetService<ControlHandler>());
            Assert.IsNotNull(host.Services.GetService<TunnelHostedService>());
            Assert.IsNotNull(host.Services.GetService<ITunnelStatusProvider>());
            Assert.IsNotNull(host.Services.GetRequiredService<TrayMenu>().Updates);
            Assert.IsNotNull(host.Services.GetRequiredService<ShellViewModel>().Updater);
        });
    }

    [TestMethod]
    public async Task CreateDesktop_StartsControlAndGatewayServices()
    {
        var host = default(IHost);
        WpfApplicationTestBase.RunShared(() =>
        {
            host = AppHostBuilder.CreateDesktop();
            _ = host.Services.GetRequiredService<TrayMenu>();
        });
        var lifetime = host!.Services.GetRequiredService<IHostApplicationLifetime>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await host.StartAsync(cts.Token);
        Assert.IsTrue(lifetime.ApplicationStarted.IsCancellationRequested);
        await cts.CancelAsync();
        await host.StopAsync(CancellationToken.None);
        Assert.IsTrue(lifetime.ApplicationStopped.IsCancellationRequested);
        host.Dispose();
    }

    [TestMethod]
    public async Task TunnelHostedService_ReactsToAuthStateChanges()
    {
        using var host = AppHostBuilder.CreateStdioHostForTests();
        var auth = DaemonTestDoubles.CreateAuthService(authenticated: true, accessToken: "token");
        var gateway = new TunnelHostedService(
            auth.Object,
            host.Services.GetRequiredService<DevTools.Daemon.Mcp.Hosting.McpEngine>(),
            host.Services.GetRequiredService<DevTools.Daemon.Mcp.Processes.IMcpPipeScanner>(),
            Options.Create(new GatewayOptions { Url = "ws://127.0.0.1:9/tunnel" }),
            NullLoggerFactory.Instance,
            host.Services,
            NullLogger<TunnelHostedService>.Instance);

        var statuses = Channel.CreateUnbounded<TunnelStatus>();
        gateway.StatusChanged += (_, args) => statuses.Writer.TryWrite(args.Status);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await gateway.StartAsync(cts.Token);
        await WaitForStatusAsync(statuses.Reader, TunnelStatus.Connecting);

        auth.Raise(a => a.StateChanged += null!, new object(), new AuthStateArgs(false));
        await WaitForStatusAsync(statuses.Reader, TunnelStatus.Disconnected);

        auth.Raise(a => a.StateChanged += null!, new object(), new AuthStateArgs(true));
        await WaitForStatusAsync(statuses.Reader, TunnelStatus.Connecting);

        await gateway.StopAsync(CancellationToken.None);
        Assert.AreEqual(TunnelStatus.Disconnected, gateway.Status);
    }

    private async Task WaitForStatusAsync(ChannelReader<TunnelStatus> statuses, TunnelStatus expected)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            while (await statuses.ReadAsync(timeout.Token) != expected)
            {
            }
        }
        catch (OperationCanceledException) when (!TestContext.CancellationToken.IsCancellationRequested)
        {
            Assert.Fail($"The tunnel never reported {expected}.");
        }
    }
}
