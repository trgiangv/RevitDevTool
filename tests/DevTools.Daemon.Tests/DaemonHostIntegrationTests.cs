using DevTools.Daemon.Auth;
using DevTools.Daemon;
using DevTools.Daemon.Control;
using DevTools.Daemon.Tunnel;
using DevTools.Daemon.Desktop.Views;
using DevTools.Daemon.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await host!.StartAsync(cts.Token);
        await cts.CancelAsync();
        await host.StopAsync(CancellationToken.None);
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

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await gateway.StartAsync(cts.Token);

        auth.Raise(a => a.StateChanged += null!, new object(), new AuthStateArgs(false));
        auth.Setup(a => a.IsAuthenticated).Returns(true);
        auth.Setup(a => a.AccessToken).Returns("token");
        auth.Raise(a => a.StateChanged += null!, new object(), new AuthStateArgs(true));

        await gateway.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task StdioHostedService_StopsWhenInputEnds()
    {
        using var host = AppHostBuilder.CreateStdioHost(["--stdio"]);
        var originalIn = Console.In;
        try
        {
            var input = new MemoryStream();
            Console.SetIn(new StreamReader(input));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var runTask = host.RunAsync(cts.Token);
            input.Close();
            await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken));
            await cts.CancelAsync();
            try { await runTask; } catch (OperationCanceledException) { }
        }
        finally
        {
            Console.SetIn(originalIn);
        }
    }
}
