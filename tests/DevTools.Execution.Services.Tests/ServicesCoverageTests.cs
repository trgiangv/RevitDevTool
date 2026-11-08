using DevTools.Execution.Abstractions;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Package = DevTools.Execution.Models.Package;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class ServicesCoverageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PyPi_and_conda_versions_parsed()
    {
        var handler = new CannedJsonHandler(new Dictionary<string, string>
        {
            ["https://pypi.org/pypi/requests-toolbelt/json"] =
                """{"info":{"name":"requests-toolbelt","version":"1.0.0","summary":"x"},"releases":{"0.9.1":[],"1.0.0":[]}}""",
            ["https://api.anaconda.org/package/conda-forge/numpy"] =
                """{"name":"numpy","owner":"conda-forge","latest_version":"2.1.3","versions":["2.1.2","2.1.3"]}"""
        });
        var checker = CreateChecker(handler);

        var result = await checker.AttachLatestVersionsAsync(
            [
                new Package(Marketplace.PyPi, " Requests_Toolbelt ", "1.0.0", "1.0.0"),
                new Package(Marketplace.CondaForge, "numpy", "2.0.0", "2.0.0")
            ],
            TestContext.CancellationToken);

        Assert.AreEqual("1.0.0", result[0].LatestVersion);
        Assert.IsTrue(result[0].IsLatest);
        Assert.AreEqual("2.1.3", result[1].LatestVersion);
        Assert.IsFalse(result[1].IsLatest);
    }

    [TestMethod]
    public async Task Missing_package_leaves_latest_null()
    {
        var checker = CreateChecker(new CannedJsonHandler([]));

        var result = await checker.AttachLatestVersionsAsync(
            [
                new Package(Marketplace.PyPi, "nope", "1.0", "1.0"),
                new Package(Marketplace.CondaForge, "nope", "1.0", "1.0")
            ],
            TestContext.CancellationToken);

        Assert.IsNull(result[0].LatestVersion);
        Assert.IsNull(result[1].LatestVersion);
        Assert.IsFalse(result[0].IsLatest);
    }

    [TestMethod]
    public async Task Same_package_fetched_once()
    {
        var handler = new CannedJsonHandler(new Dictionary<string, string>
        {
            ["https://pypi.org/pypi/six/json"] = """{"info":{"version":"1.17.0"}}"""
        });
        var checker = CreateChecker(handler);

        var result = await checker.AttachLatestVersionsAsync(
            [
                new Package(Marketplace.PyPi, "six", "1.16.0", "1.16.0"),
                new Package(Marketplace.PyPi, "SIX", "1.17.0", "1.17.0")
            ],
            TestContext.CancellationToken);

        Assert.AreEqual(1, handler.RequestCount);
        Assert.AreEqual("1.17.0", result[0].LatestVersion);
        Assert.IsTrue(result[1].IsLatest);
    }

    [TestMethod]
    public async Task Empty_package_list_returns_empty()
    {
        var checker = CreateChecker(new CannedJsonHandler([]));

        var result = await checker.AttachLatestVersionsAsync([], TestContext.CancellationToken);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void PackageTreeNodes_BuildHierarchyAndRoundTrip()
    {
        var package = new Package(Marketplace.PyPi, "requests", "2.32.0", "2.32.0", true, "2.32.1", false);
        var item = new PackageItemNode(package);
        var conda = new MarketplaceNode(Marketplace.CondaForge);
        var nuget = new MarketplaceNode(Marketplace.NuGet);

        conda.Children.Add(item);
        Assert.AreEqual("requests (2.32.0)", item.Name);
        Assert.AreEqual("Conda-forge", conda.Name);
        Assert.AreEqual("NuGet", nuget.Name);
        Assert.IsTrue(item.IsProtected);
        Assert.IsFalse(item.IsLatest);

        var roundTrip = item.ToRuntimePackage();
        Assert.AreEqual(package.PackageId, roundTrip.PackageId);
        Assert.AreEqual(package.Marketplace, roundTrip.Marketplace);
    }

    [TestMethod]
    public async Task PackageService_RemovePyPiPackageAndMarketplace_DoesNotThrow()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        using var sp = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var initializer = sp.GetRequiredService<PythonInitializer>();
        await initializer.InitializeAsync();
        ExecutionTestHelpers.EnsureDevtoolNamespace(initializer);

        var service = sp.GetRequiredService<IPackageService>();
        await service.RemovePackageAsync(new Package(Marketplace.PyPi, "six", null, null), TestContext.CancellationToken);
        await service.RemoveAllAsync(Marketplace.PyPi, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task PackageService_ListInstalled_WithPixi_ReturnsPackages()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        using var sp = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var initializer = sp.GetRequiredService<PythonInitializer>();
        await initializer.InitializeAsync();
        ExecutionTestHelpers.EnsureDevtoolNamespace(initializer);

        var packages = await sp.GetRequiredService<IPackageService>().ListInstalledPackagesAsync(TestContext.CancellationToken);

        Assert.IsNotEmpty(packages);
    }

    [TestMethod]
    public async Task PackageService_WithPixiProvider_ListInstalledPackages_IncludesPythonSide()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        using var provider = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var initializer = provider.GetRequiredService<PythonInitializer>();
        await initializer.InitializeAsync();
        ExecutionTestHelpers.EnsureDevtoolNamespace(initializer);

        var service = provider.GetRequiredService<IPackageService>();
        var packages = await service.ListInstalledPackagesAsync(TestContext.CancellationToken);

        Assert.IsNotNull(packages);
    }

    [TestMethod]
    public async Task PackageService_UpdateLatestAsync_ForPythonPackage_DoesNotThrow()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        using var sp = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var initializer = sp.GetRequiredService<PythonInitializer>();
        await initializer.InitializeAsync();
        ExecutionTestHelpers.EnsureDevtoolNamespace(initializer);

        var service = sp.GetRequiredService<IPackageService>();
        await service.UpdateLatestAsync(new Package(Marketplace.PyPi, "six", null, null), TestContext.CancellationToken);
        await service.RepairAsync(new Package(Marketplace.PyPi, "six", null, null), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task NugetManager_ResolvePackageDlls_ReturnsDllPaths()
    {
        var manager = new NugetManager(NullLogger<NugetManager>.Instance);

        var dlls = await manager.ResolvePackageDllsAsync("Newtonsoft.Json", "13.0.3", TestContext.CancellationToken);

        Assert.IsNotEmpty(dlls);
        foreach (var path in dlls)
            Assert.IsTrue(path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task NetworkService_GetBytesAsync_DownloadsFromLocalServer()
    {
        var port = GetFreeTcpPort();
        using var listener = new System.Net.HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var url = $"http://127.0.0.1:{port}/bytes";
        var payload = new byte[] { 1, 2, 3 };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
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
            var bytes = await NetworkService.GetBytesAsync(url, TestContext.CancellationToken);
            CollectionAssert.AreEqual(payload, bytes);
        }
        finally
        {
            listener.Stop();
            listener.Close();
            cts.Cancel();
        }
    }

    [TestMethod]
    public async Task NetworkService_GetJsonDocumentAsync_ParsesLocalPayload()
    {
        var port = GetFreeTcpPort();
        using var listener = new System.Net.HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var url = $"http://127.0.0.1:{port}/json";
        const string payload = """{"info":{"version":"9.9.9"}}""";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
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
            using var doc = await NetworkService.GetJsonDocumentAsync(url, TestContext.CancellationToken);
            Assert.IsNotNull(doc);
            Assert.AreEqual("9.9.9", doc!.RootElement.GetProperty("info").GetProperty("version").GetString());
        }
        finally
        {
            listener.Stop();
            listener.Close();
            cts.Cancel();
        }
    }

    [TestMethod]
    public async Task NetworkService_GetStringAsync_DownloadsFromLocalServer()
    {
        var port = GetFreeTcpPort();
        using var listener = new System.Net.HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var url = $"http://127.0.0.1:{port}/text";
        const string payload = "hello-network";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
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
            var text = await NetworkService.GetStringAsync(url, TestContext.CancellationToken);
            Assert.AreEqual(payload, text);
        }
        finally
        {
            listener.Stop();
            listener.Close();
            cts.Cancel();
        }
    }

    [TestMethod]
    public async Task NetworkService_GetJsonDocumentAsync_ReturnsNull_OnNotFound()
    {
        var port = GetFreeTcpPort();
        using var listener = new System.Net.HttpListener();
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
            using var doc = await NetworkService.GetJsonDocumentAsync(url, TestContext.CancellationToken);
            Assert.IsNull(doc);
        }
        finally
        {
            listener.Stop();
            listener.Close();
            cts.Cancel();
        }
    }

    [TestMethod]
    public void ExecutionGuardContext_SuppressMode_CanBeSetAndRead()
    {
        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
        Assert.AreEqual(ExecutionGuardMode.Suppress, ExecutionGuardContext.Mode);
        ExecutionGuardContext.Mode = ExecutionGuardMode.Passthrough;
    }

    private static PackageVersionChecker CreateChecker(HttpMessageHandler handler) =>
        new(
            new NugetManager(NullLogger<NugetManager>.Instance),
            NullLogger<PackageVersionChecker>.Instance,
            new HttpClient(handler));

    private sealed class CannedJsonHandler(Dictionary<string, string> responses) : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(responses.TryGetValue(request.RequestUri!.ToString(), out var json)
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json) }
                : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
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
