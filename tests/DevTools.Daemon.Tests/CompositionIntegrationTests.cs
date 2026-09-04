using DevTools.Daemon.Auth;
using DevTools.Daemon.Composition;
using DevTools.Daemon.Control;
using DevTools.Daemon.Gateway;
using DevTools.Daemon.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DevTools.Daemon.Tests;

public sealed class CompositionIntegrationTests
{
    [Fact]
    public void CreateDesktop_RegistersDesktopServices()
    {
        using var host = ServerHostBuilder.CreateDesktop();
        Assert.NotNull(host.Services.GetService<ControlPipeHandler>());
        Assert.NotNull(host.Services.GetService<GatewayHostedService>());
        Assert.NotNull(host.Services.GetService<ITunnelStatusProvider>());
    }

    [Fact]
    public async Task CreateDesktop_StartsControlAndGatewayServices()
    {
        using var host = ServerHostBuilder.CreateDesktop();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await host.StartAsync(cts.Token);
        await cts.CancelAsync();
        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task GatewayHostedService_ReactsToAuthStateChanges()
    {
        using var host = ServerHostBuilder.CreateStdioHostForTests();
        var auth = DaemonTestDoubles.CreateAuthService(authenticated: true, accessToken: "token");
        var gateway = new GatewayHostedService(
            auth.Object,
            host.Services.GetRequiredService<DevTools.Mcp.Server.Hosting.McpEngine>(),
            host.Services.GetRequiredService<DevTools.Mcp.Client.IMcpPipeScanner>(),
            Options.Create(new GatewayOptions { Url = "ws://127.0.0.1:9/tunnel" }),
            NullLoggerFactory.Instance,
            host.Services,
            NullLogger<GatewayHostedService>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await gateway.StartAsync(cts.Token);

        auth.Raise(a => a.StateChanged += null!, new object(), new AuthStateArgs(false));
        auth.Setup(a => a.IsAuthenticated).Returns(true);
        auth.Setup(a => a.AccessToken).Returns("token");
        auth.Raise(a => a.StateChanged += null!, new object(), new AuthStateArgs(true));

        await gateway.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StdioHostedService_StopsWhenInputEnds()
    {
        using var host = ServerHostBuilder.CreateStdioHost(["--stdio"]);
        var originalIn = Console.In;
        try
        {
            var input = new MemoryStream();
            Console.SetIn(new StreamReader(input));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var runTask = host.RunAsync(cts.Token);
            input.Close();
            await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            await cts.CancelAsync();
            try { await runTask; } catch (OperationCanceledException) { }
        }
        finally
        {
            Console.SetIn(originalIn);
        }
    }
}
