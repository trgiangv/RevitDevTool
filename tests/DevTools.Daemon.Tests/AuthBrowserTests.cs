using Duende.IdentityModel.OidcClient.Browser;
using DevTools.Daemon.Auth;

namespace DevTools.Daemon.Tests;

public sealed class AuthBrowserTests
{
    [Fact]
    public async Task InvokeAsync_Cancellation_ReturnsTimeoutOrUnknownError()
    {
        var port = GetFreePort();
        var callback = $"http://127.0.0.1:{port}/callback";
        var browser = new AuthBrowser(new AuthOptions { LoopbackPort = port });
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(100);

        var result = await browser.InvokeAsync(
            new BrowserOptions("about:blank", callback),
            cts.Token);

        Assert.True(
            result.ResultType is BrowserResultType.Timeout or BrowserResultType.UnknownError,
            $"Unexpected result type: {result.ResultType}");
    }

    [Fact]
    public async Task InvokeAsync_InvalidPrefix_ReturnsUnknownError()
    {
        var browser = new AuthBrowser(new AuthOptions { LoopbackPort = 1 });
        var result = await browser.InvokeAsync(
            new BrowserOptions("about:blank", "http://127.0.0.1:1/callback"),
            TestContext.Current.CancellationToken);

        Assert.Equal(BrowserResultType.UnknownError, result.ResultType);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }
}
