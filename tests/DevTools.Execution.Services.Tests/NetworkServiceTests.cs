using System.Net.Sockets;
using System.Net;
using System.Text;
using DevTools.Execution.Services;
using DevTools.Hosting;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class NetworkServiceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Configure_SetsUserAgentForHost()
    {
        NetworkService.Configure(HostApp.Revit);
        NetworkService.Configure(HostApp.AutoCad);
    }

    [TestMethod]
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

        Assert.AreEqual("ok", result);
        Assert.AreEqual(2, attempts);
    }

    [TestMethod]
    public async Task GetJsonDocumentAsync_ReturnsNullForNonSuccessStatus()
    {
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var url = $"http://127.0.0.1:{port}/missing";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
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
            var document = await NetworkService.GetJsonDocumentAsync(url, TestContext.CancellationToken);
            Assert.IsNull(document);
        }
        finally
        {
            listener.Stop();
            listener.Close();
            cts.Cancel();
        }
    }

    [TestMethod]
    public async Task GetStringAsync_ReturnsPayload()
    {
        var port = GetFreeTcpPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var url = $"http://127.0.0.1:{port}/ok";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
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
            var payload = await NetworkService.GetStringAsync(url, TestContext.CancellationToken);
            Assert.AreEqual("hello", payload);
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
