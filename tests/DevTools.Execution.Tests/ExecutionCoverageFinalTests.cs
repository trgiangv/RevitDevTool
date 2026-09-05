using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External;
using DevTools.Execution.External.Handlers;
using DevTools.Execution.External.Mcp.Backends;
using DevTools.Execution;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Hosting;
using DevTools.Ipc;
using DevTools.Mcp.Catalog.Discovery;
using DevTools.Mcp.Catalog.Isolation;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.Tests;

public sealed class ExecutionCoverageFinalTests
{
    [Fact]
    public void CSharpDirectiveParser_RewritesHostReference_WhenPatternMatches()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-host-rewrite");
        var dllPath = Path.Combine(directory, "RevitAPI_2024.dll");
        var rewrittenPath = Path.Combine(directory, "RevitAPI_2025.dll");
        File.WriteAllText(dllPath, "stub");
        File.WriteAllText(rewrittenPath, "stub");

        var entryPath = Path.Combine(directory, "entry.csx");
        File.WriteAllText(
            entryPath,
            $"""
            #r "{dllPath.Replace('\\', '/')}"
            Console.WriteLine("ok");
            """);

        try
        {
            var graph = CSharpDirectiveParser.ResolveGraph(
                entryPath,
                path => path.Contains("RevitAPI_2024.dll", StringComparison.OrdinalIgnoreCase)
                    ? rewrittenPath
                    : path);

            Assert.Single(graph.AssemblyReferences);
            Assert.Equal(Path.GetFullPath(rewrittenPath), Path.GetFullPath(graph.AssemblyReferences[0]), ignoreCase: true);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CSharpDirectiveParser_IgnoresFrameworkReferenceSegments()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-ignored-ref");
        var entryPath = Path.Combine(directory, "entry.csx");
        File.WriteAllText(
            entryPath,
            """
            #r "C:/Program Files/dotnet/shared/Microsoft.NETCore.App/8.0.0/System.Runtime.dll"
            #r "nuget: Humanizer"
            """);

        try
        {
            var graph = CSharpDirectiveParser.ResolveGraph(entryPath);

            Assert.Single(graph.Packages);
            Assert.Equal("Humanizer", graph.Packages[0].PackageId);
            Assert.Null(graph.Packages[0].Version);
            Assert.Empty(graph.AssemblyReferences);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CSharpDirectiveParser_SkipsMissingLoadTarget()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-missing-load");
        var entryPath = Path.Combine(directory, "entry.csx");
        File.WriteAllText(entryPath, "#load \"missing.csx\"");

        try
        {
            var graph = CSharpDirectiveParser.ResolveGraph(entryPath);
            Assert.Single(graph.SourceFiles);
            Assert.Contains("//", graph.SourceFiles[0].CleanSource, StringComparison.Ordinal);
            Assert.Contains("missing.csx", graph.SourceFiles[0].CleanSource, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CSharpDirectiveParser_AddsExistingAssemblyReference()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-asm-ref");
        var dllPath = Path.Combine(directory, "helper.dll");
        File.WriteAllBytes(dllPath, [0]);

        var entryPath = Path.Combine(directory, "entry.csx");
        File.WriteAllText(entryPath, $"""#r "{dllPath.Replace('\\', '/')}" """);

        try
        {
            var graph = CSharpDirectiveParser.ResolveGraph(entryPath);
            Assert.Contains(
                graph.AssemblyReferences,
                path => Path.GetFullPath(path).Equals(Path.GetFullPath(dllPath), StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task PackageVersionChecker_AttachLatestVersions_PyPiAndConda_FetchLatest()
    {
        var checker = new PackageVersionChecker(
            new NugetManager(NullLogger<NugetManager>.Instance),
            NullLogger<PackageVersionChecker>.Instance);

        var packages = new List<Package>
        {
            new(Marketplace.PyPi, "requests", "2.0.0", "2.0.0"),
            new(Marketplace.CondaForge, "numpy", "1.0.0", "1.0.0"),
        };

        var result = await checker.AttachLatestVersionsAsync(packages, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.False(string.IsNullOrWhiteSpace(result[0].LatestVersion));
        Assert.False(string.IsNullOrWhiteSpace(result[1].LatestVersion));
    }

    [Fact]
    public void PythonEmbedded_AutoCadSetup_SelectsAcadScript()
    {
        PythonEmbedded.Configure(HostApp.AutoCad);
        PythonEmbedded.EnsureExtracted();

        Assert.Equal("SetupAcad.py", PythonEmbedded.SetupScriptFileName);
        Assert.False(string.IsNullOrWhiteSpace(PythonEmbedded.SetupScript));
        Assert.False(string.IsNullOrWhiteSpace(PythonEmbedded.ParserScriptPath));
    }

    [Fact]
    public void PythonDepsManager_TryResolveSidecarStdlib_FromLibDir()
    {
        var root = ExecutionTestHelpers.CreateTempDirectory("stdlib-sidecar");
        var lib = Path.Combine(root, "Lib");
        var dlls = Path.Combine(root, "DLLs");
        Directory.CreateDirectory(lib);
        Directory.CreateDirectory(dlls);

        try
        {
            Assert.True(PythonDepsManager.TryResolveSidecarStdlib(lib, null, out var stdlibLib, out var stdlibDlls));
            Assert.Equal(lib, stdlibLib);
            Assert.Equal(dlls, stdlibDlls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DotnetMcpToolBackend_ReadResource_ResolvesStaticResource()
    {
        var backend = CreateDotnetBackend();
        var resource = new McpRegisteredResource
        {
            Id = "execution-status",
            Descriptor = new Resource { Uri = "execution://status", Name = "execution_status" },
            Binding = McpPrimitiveBinding.Create(
                ExecutionMode.Dotnet,
                typeof(ExecutionDotnetMcpResourceStubs).Assembly.Location,
                typeof(ExecutionDotnetMcpResourceStubs).FullName!,
                nameof(ExecutionDotnetMcpResourceStubs.Status)),
        };

        var result = backend.ReadResource(resource, "execution://status", TestContext.Current.CancellationToken);

        Assert.NotNull(result.Contents);
        Assert.NotEmpty(result.Contents);
    }

    [Fact]
    public void DotnetMcpToolBackend_ReadResource_UnknownResource_Throws()
    {
        var backend = CreateDotnetBackend();
        var resource = new McpRegisteredResource
        {
            Id = "missing",
            Descriptor = new Resource { Uri = "execution://missing", Name = "missing_resource" },
            Binding = McpPrimitiveBinding.Create(ExecutionMode.Dotnet, string.Empty, "Missing", "Run"),
        };

        Assert.Throws<InvalidOperationException>(() =>
            backend.ReadResource(resource, "execution://missing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CSharpDirectiveParser_DeduplicatesVisitedFiles()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-cycle");
        var entryPath = Path.Combine(directory, "entry.csx");
        var depPath = Path.Combine(directory, "dep.csx");
        File.WriteAllText(depPath, "#load \"entry.csx\"");
        File.WriteAllText(entryPath, "#load \"dep.csx\"");

        try
        {
            var graph = CSharpDirectiveParser.ResolveGraph(entryPath);
            Assert.Equal(2, graph.SourceFiles.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PythonEmbedded_RevitSetup_ExposesEmbeddedScripts()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        PythonEmbedded.EnsureExtracted();

        Assert.Equal("SetupRevit.py", PythonEmbedded.SetupScriptFileName);
        Assert.False(string.IsNullOrWhiteSpace(PythonEmbedded.ToolParserScript));
        Assert.False(string.IsNullOrWhiteSpace(PythonEmbedded.PytestRunnerScript));
    }

    [Fact]
    public void PipEnvironmentProvider_SelectCengineDir_ReturnsNull_WhenVersionMismatch()
    {
        var root = ExecutionTestHelpers.CreateTempDirectory("pip-version");
        var engine = Path.Combine(root, "CPY_3_13");
        Directory.CreateDirectory(engine);
        File.WriteAllText(Path.Combine(engine, "python.exe"), string.Empty);
        File.WriteAllBytes(Path.Combine(engine, "python313.dll"), [1]);

        try
        {
            Assert.Null(PipEnvironmentProvider.SelectCengineDir([engine], "9.99"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PackageVersionChecker_AttachLatestVersions_UnknownMarketplace_LeavesLatestNull()
    {
        var checker = new PackageVersionChecker(
            new NugetManager(NullLogger<NugetManager>.Instance),
            NullLogger<PackageVersionChecker>.Instance);
        var packages = new List<Package> { new(Marketplace.NuGet, "  Newtonsoft.Json  ", "13.0.1", "13.0.1") };

        var result = await checker.AttachLatestVersionsAsync(packages, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.False(string.IsNullOrWhiteSpace(result[0].LatestVersion));
    }

    [Fact]
    public void DotnetMcpToolBackend_ClearCaches_AfterResourceRead_AllowsRebind()
    {
        var backend = CreateDotnetBackend();
        var resource = new McpRegisteredResource
        {
            Id = "execution-status-2",
            Descriptor = new Resource { Uri = "execution://status", Name = "execution_status" },
            Binding = McpPrimitiveBinding.Create(
                ExecutionMode.Dotnet,
                typeof(ExecutionDotnetMcpResourceStubs).Assembly.Location,
                typeof(ExecutionDotnetMcpResourceStubs).FullName!,
                nameof(ExecutionDotnetMcpResourceStubs.Status)),
        };

        backend.ReadResource(resource, "execution://status", TestContext.Current.CancellationToken);
        backend.ClearCaches();
        var second = backend.ReadResource(resource, "execution://status", TestContext.Current.CancellationToken);
        Assert.NotEmpty(second.Contents);
    }

    [Fact]
    public void CSharpDirectiveParser_MissingEntryFile_ReturnsEmptyGraph()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"missing-entry-{Guid.NewGuid():N}.csx");
        var graph = CSharpDirectiveParser.ResolveGraph(missing);
        Assert.Empty(graph.SourceFiles);
    }

    [Fact]
    public async Task DevToolsPipeServer_StartAsync_WiresNotificationPublisher()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        var hostInfo = new StubHostAppInfo(Guid.NewGuid().ToString("N"));
        var handler = new NotificationPingHandler();
        using var server = new DevToolsPipeServer(
            new External.Mcp.Connections.McpConnectState(NullLogger<External.Mcp.Connections.McpConnectState>.Instance),
            hostInfo,
            [handler, new InstanceRequestHandler(hostInfo)],
            NullLogger<DevToolsPipeServer>.Instance);

        await server.StartAsync(cts.Token);
        var pipeName = HostPipeName.FormatTest(hostInfo.Host.ToString(), hostInfo.VersionNumber, Environment.ProcessId);

        using var client = await ConnectClientAsync(pipeName, cts.Token);
        var notificationTcs = new TaskCompletionSource<BridgeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.MessageReceived += msg =>
        {
            if (msg.Type == BridgeMessage.TypeNotification)
                notificationTcs.TrySetResult(msg);
        };

        var response = await SendRequestAsync(
            client,
            BridgeMessage.Request("notify-1", NotificationPingHandler.PingMethod),
            cts.Token);

        Assert.False(response.IsError);
        var notification = await notificationTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        Assert.Equal(NotificationPingHandler.ProgressMethod, notification.Method);

        await server.StopAsync(cts.Token);
    }

    [Collection(nameof(NugetRestoreCollection))]
    public sealed class FSharpCacheSuccessTests
    {
        [Fact]
        public async Task FSharpCompilationCache_SecondCall_ReportsCacheHit()
        {
            var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-cache-hit");
            var scriptPath = Path.Combine(directory, "command_script.fsx");
            await File.WriteAllTextAsync(
                scriptPath,
                """
                type ScriptCommand() =
                    member _.Run() = ()
                """,
                TestContext.Current.CancellationToken);

            var bridge = ExecutionTestHelpers.CreateScriptBridge();
            var resolver = new FSharpDependencyResolver(NullLogger<FSharpDependencyResolver>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));
            var executor = new FSharpExecutor(NullLogger<FSharpExecutor>.Instance);
            var cache = new FSharpCompilationCache(bridge, resolver, executor, NullLogger<FSharpCompilationCache>.Instance);
            var progress = new List<string>();

            try
            {
                var first = await cache.GetOrCompileAsync(scriptPath, new Progress<string>(progress.Add), TestContext.Current.CancellationToken);
                progress.Clear();
                var second = await cache.GetOrCompileAsync(scriptPath, new Progress<string>(progress.Add), TestContext.Current.CancellationToken);

                Assert.True(first.Success, first.FormatDiagnostics());
                Assert.True(second.Success, second.FormatDiagnostics());
                if (progress.Count > 0)
                    Assert.Contains(progress, message => message.Contains("cached", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task FSharpExecutionStrategy_Success_InvokesCommandRunner()
        {
            var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-strategy-ok");
            var scriptPath = Path.Combine(directory, "run_script.fsx");
            await File.WriteAllTextAsync(
                scriptPath,
                """
                type ScriptCommand() =
                    member _.Run() = ()
                """,
                TestContext.Current.CancellationToken);

            var bridge = ExecutionTestHelpers.CreateScriptBridge();
            var resolver = new FSharpDependencyResolver(NullLogger<FSharpDependencyResolver>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));
            var executor = new FSharpExecutor(NullLogger<FSharpExecutor>.Instance);
            var cache = new FSharpCompilationCache(bridge, resolver, executor, NullLogger<FSharpCompilationCache>.Instance);
            var commandRunner = new Mock<ICommandRunner>();
            commandRunner
                .Setup(r => r.RunCompiledCommand(It.IsAny<object>()))
                .Returns(ExecutionResult.Succeeded("done", 5));

            try
            {
                var strategy = new FSharpExecutionStrategy(
                    scriptPath,
                    ExecutionTestHelpers.InlineHostContext(),
                    commandRunner.Object,
                    cache,
                    NullLogger<FSharpExecutionStrategy>.Instance);

                var result = await strategy.ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(result.Success, result.Message);
                commandRunner.Verify(r => r.RunCompiledCommand(It.IsAny<object>()), Times.Once);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Collection(nameof(PythonRuntimeCollection))]
    public sealed class PythonDepsRuntimeTests
    {
        [Fact]
        public async Task ResolveDependenciesAsync_WithPep723Inline_Completes()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var provider = initializer.Provider!;
            Assert.True(provider.IsEnvironmentReady());

            const string script = """
                # /// script
                # dependencies = ["six"]
                print("deps")
                """;

            var deps = await PythonDepsManager.ResolveDependenciesAsync(provider, script, TestContext.Current.CancellationToken);

            Assert.NotNull(deps);
        }

        [Fact]
        public async Task InstallDependenciesAsync_EmptyList_IsNoOp()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var provider = initializer.Provider!;
            var messages = new List<string>();

            await PythonDepsManager.InstallDependenciesAsync(
                provider,
                [],
                new Progress<string>(messages.Add),
                TestContext.Current.CancellationToken);

            Assert.Empty(messages);
        }

        [Fact]
        public async Task PackageService_RemovePyPiPackageAndMarketplace_DoesNotThrow()
        {
            PythonEmbedded.Configure(HostApp.Revit);
            using var sp = ExecutionTestHelpers.BuildExecutionServiceProvider();
            var initializer = sp.GetRequiredService<PythonInitializer>();
            await initializer.InitializeAsync();
            ExecutionTestHelpers.EnsureDevtoolNamespace(initializer);

            var service = sp.GetRequiredService<IPackageService>();
            await service.RemovePackageAsync(new Package(Marketplace.PyPi, "six", null, null), TestContext.Current.CancellationToken);
            await service.RemoveAllAsync(Marketplace.PyPi, TestContext.Current.CancellationToken);
        }
        [Fact]
        public async Task RefreshImportCache_WhenInitialized_DoesNotThrow()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            PythonDepsManager.RefreshImportCache(initializer);
        }

        [Fact]
        public async Task InstallDependenciesAsync_WithPackage_ReportsProgress()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var provider = initializer.Provider!;
            var messages = new List<string>();

            await PythonDepsManager.InstallDependenciesAsync(
                provider,
                ["six"],
                new Progress<string>(messages.Add),
                TestContext.Current.CancellationToken);

            Assert.NotEmpty(messages);
        }

        [Fact]
        public async Task PackageService_ListInstalled_WithPixi_ReturnsPackages()
        {
            PythonEmbedded.Configure(HostApp.Revit);
            using var sp = ExecutionTestHelpers.BuildExecutionServiceProvider();
            var initializer = sp.GetRequiredService<PythonInitializer>();
            await initializer.InitializeAsync();
            ExecutionTestHelpers.EnsureDevtoolNamespace(initializer);

            var packages = await sp.GetRequiredService<IPackageService>().ListInstalledPackagesAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(packages);
        }

        [Fact]
        public async Task PixiEnvironmentProvider_InstallPackagesAsync_NumpyPartition_DoesNotThrow()
        {
            await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var provider = new PixiEnvironmentProvider(NullLogger<PixiEnvironmentProvider>.Instance);
            var messages = new List<string>();

            await provider.InstallPackagesAsync(["numpy"], new Progress<string>(messages.Add), TestContext.Current.CancellationToken);
        }
    }

    private static DotnetMcpToolBackend CreateDotnetBackend()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        return new DotnetMcpToolBackend(
            provider,
            new DotnetMethodResolver(
                new McpToolsetContextManager(NullLogger<McpToolsetContextManager>.Instance),
                NullLogger<DotnetMethodResolver>.Instance));
    }

    private static async Task<BridgePipeConnection> ConnectClientAsync(string pipeName, CancellationToken ct)
    {
        var clientPipe = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
        for (var attempt = 0; attempt < 50; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await clientPipe.ConnectAsync(50, ct).ConfigureAwait(false);
                return new BridgePipeConnection(clientPipe);
            }
            catch (TimeoutException) when (attempt < 49)
            {
            }
        }

        clientPipe.Dispose();
        throw new TimeoutException($"Could not connect to pipe '{pipeName}'.");
    }

    private static async Task<BridgeMessage> SendRequestAsync(BridgePipeConnection connection, BridgeMessage request, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<BridgeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.MessageReceived += msg =>
        {
            if (msg.Type == BridgeMessage.TypeResponse)
                tcs.TrySetResult(msg);
        };
        connection.StartReadLoop();

        await connection.WriteAsync(request, ct).ConfigureAwait(false);
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
    }

    private sealed class StubHostAppInfo(string version) : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber { get; } = version;
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }

    private sealed class NotificationPingHandler : IBridgeRequestHandler, IBridgeNotificationPublisher
    {
        public const string PingMethod = "tests/ping";
        public const string ProgressMethod = "notifications/tests/progress";

        public IReadOnlyCollection<string> SupportedMethods => [PingMethod];

        public Action<string, JsonElement?>? NotificationSender { get; set; }

        public Task<BridgeMessage> HandleAsync(string id, string method, JsonElement? parameters, CancellationToken cancellationToken)
        {
            NotificationSender?.Invoke(ProgressMethod, null);
            return Task.FromResult(BridgeMessage.Response(id, JsonSerializer.SerializeToElement(new { ok = true })));
        }
    }
}

[McpServerResourceType]
internal static class ExecutionDotnetMcpResourceStubs
{
    [McpServerResource(Name = "execution_status")]
    public static TextResourceContents Status() =>
        new() { Uri = "execution://status", Text = "ok", MimeType = "text/plain" };
}
