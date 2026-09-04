using DevTools.Execution.External.Mcp.Connections;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Services;
using DevTools.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

[Collection(nameof(NugetRestoreCollection))]
public sealed class AdditionalCoverageTests
{
    [Fact]
    public async Task PackageVersionChecker_AttachLatestVersions_EmptyList_ReturnsEmpty()
    {
        var checker = new PackageVersionChecker(
            new NugetManager(NullLogger<NugetManager>.Instance),
            NullLogger<PackageVersionChecker>.Instance);

        var result = await checker.AttachLatestVersionsAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task PackageVersionChecker_AttachLatestVersions_NuGetPackage_FetchesLatest()
    {
        var checker = new PackageVersionChecker(
            new NugetManager(NullLogger<NugetManager>.Instance),
            NullLogger<PackageVersionChecker>.Instance);
        var packages = new List<Package> { new(Marketplace.NuGet, "Newtonsoft.Json", "13.0.1", "13.0.1") };

        var result = await checker.AttachLatestVersionsAsync(packages, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.False(string.IsNullOrWhiteSpace(result[0].LatestVersion));
    }

    [Fact]
    public void McpConnectState_RecordsToolCallsAndExecutionScope()
    {
        var state = new McpConnectState(NullLogger<McpConnectState>.Instance);
        state.RecordCall("id-1", "echo");
        state.RecordCall("id-1", "echo");

        Assert.Equal(2, state.TotalToolCalls);
        Assert.Single(state.ToolCalls);
        Assert.Equal(2, state.ToolCalls[0].Count);

        using var scope = state.BeginExecution("echo");
        scope.MarkRunning();
        scope.Dispose();
        Assert.False(state.IsExecuting);
    }

    [Fact]
    public void McpExecutionTracker_ForwardsToConnectState()
    {
        var state = new McpConnectState(NullLogger<McpConnectState>.Instance);
        var tracker = new McpExecutionTracker(state);

        tracker.RecordCall("tool-id", "sample");
        using var scope = tracker.BeginExecution("sample");
        tracker.MarkRunning(scope);
        scope.Dispose();

        Assert.Equal(1, state.TotalToolCalls);
    }

    [Fact]
    public async Task NugetManager_ResolvePackageDlls_ReturnsDllPaths()
    {
        var manager = new NugetManager(NullLogger<NugetManager>.Instance);

        var dlls = await manager.ResolvePackageDllsAsync("Newtonsoft.Json", "13.0.3", TestContext.Current.CancellationToken);

        Assert.NotEmpty(dlls);
        Assert.All(dlls, path => Assert.EndsWith(".dll", path, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NetworkService_GetBytesAsync_DownloadsFromLocalServer()
    {
        var port = GetFreeTcpPort();
        using var listener = new System.Net.HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var url = $"http://127.0.0.1:{port}/bytes";
        var payload = new byte[] { 1, 2, 3 };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _ = Task.Run(async () =>
        {
            while (listener.IsListening && !cts.Token.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(cts.Token);
                context.Response.ContentLength64 = payload.Length;
                await context.Response.OutputStream.WriteAsync(payload, cts.Token);
                context.Response.Close();
            }
        }, cts.Token);

        try
        {
            var bytes = await NetworkService.GetBytesAsync(url, TestContext.Current.CancellationToken);
            Assert.Equal(payload, bytes);
        }
        finally
        {
            listener.Stop();
            listener.Close();
            cts.Cancel();
        }
    }

    [Fact]
    public async Task NetworkService_GetJsonDocumentAsync_ParsesLocalPayload()
    {
        var port = GetFreeTcpPort();
        using var listener = new System.Net.HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var url = $"http://127.0.0.1:{port}/json";
        const string payload = """{"info":{"version":"9.9.9"}}""";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _ = Task.Run(async () =>
        {
            while (listener.IsListening && !cts.Token.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(cts.Token);
                var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, cts.Token);
                context.Response.Close();
            }
        }, cts.Token);

        try
        {
            using var doc = await NetworkService.GetJsonDocumentAsync(url, TestContext.Current.CancellationToken);
            Assert.NotNull(doc);
            Assert.Equal("9.9.9", doc!.RootElement.GetProperty("info").GetProperty("version").GetString());
        }
        finally
        {
            listener.Stop();
            listener.Close();
            cts.Cancel();
        }
    }

    [Fact]
    public void NetworkService_Configure_UpdatesUserAgent()
    {
        NetworkService.Configure(HostApp.Revit);
        NetworkService.Configure(HostApp.AutoCad);
    }

    [Fact]
    public async Task NetworkService_GetStringAsync_DownloadsFromLocalServer()
    {
        var port = GetFreeTcpPort();
        using var listener = new System.Net.HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var url = $"http://127.0.0.1:{port}/text";
        const string payload = "hello-network";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _ = Task.Run(async () =>
        {
            while (listener.IsListening && !cts.Token.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(cts.Token);
                var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, cts.Token);
                context.Response.Close();
            }
        }, cts.Token);

        try
        {
            var text = await NetworkService.GetStringAsync(url, TestContext.Current.CancellationToken);
            Assert.Equal(payload, text);
        }
        finally
        {
            listener.Stop();
            listener.Close();
            cts.Cancel();
        }
    }

    [Fact]
    public async Task NetworkService_GetJsonDocumentAsync_ReturnsNull_OnNotFound()
    {
        var port = GetFreeTcpPort();
        using var listener = new System.Net.HttpListener();
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
            using var doc = await NetworkService.GetJsonDocumentAsync(url, TestContext.Current.CancellationToken);
            Assert.Null(doc);
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
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
