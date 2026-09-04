using System.Net.Sockets;
using System.Net;
using System.Text;
using DevTools.Execution.Services;
using DevTools.Hosting;

namespace DevTools.Execution.Tests;

public sealed class NetworkServiceTests
{
    [Fact]
    public void Configure_SetsUserAgentForHost()
    {
        NetworkService.Configure(HostApp.Revit);
        NetworkService.Configure(HostApp.AutoCad);
    }

    [Fact]
    public async Task WithRetryAsync_RetriesTransientFailures()
    {
        var attempts = 0;
        var result = await NetworkService.WithRetryAsync(async () =>
        {
            attempts++;
            if (attempts < 2)
                throw new HttpRequestException("transient");

            await Task.CompletedTask;
            return "ok";
        }, maxRetries: 3, baseDelayMs: 1);

        Assert.Equal("ok", result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task GetJsonDocumentAsync_ReturnsNullForNonSuccessStatus()
    {
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var url = $"http://127.0.0.1:{port}/missing";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _ = Task.Run(async () =>
        {
            while (listener.IsListening && !cts.Token.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(cts.Token);
                context.Response.StatusCode = 404;
                context.Response.Close();
            }
        }, cts.Token);

        try
        {
            var document = await NetworkService.GetJsonDocumentAsync(url, TestContext.Current.CancellationToken);
            Assert.Null(document);
        }
        finally
        {
            listener.Stop();
            listener.Close();
            cts.Cancel();
        }
    }

    [Fact]
    public async Task GetStringAsync_ReturnsPayload()
    {
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var url = $"http://127.0.0.1:{port}/ok";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _ = Task.Run(async () =>
        {
            while (listener.IsListening && !cts.Token.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(cts.Token);
                var bytes = Encoding.UTF8.GetBytes("hello");
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, cts.Token);
                context.Response.Close();
            }
        }, cts.Token);

        try
        {
            var payload = await NetworkService.GetStringAsync(url, TestContext.Current.CancellationToken);
            Assert.Equal("hello", payload);
        }
        finally
        {
            listener.Stop();
            listener.Close();
            cts.Cancel();
        }
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
