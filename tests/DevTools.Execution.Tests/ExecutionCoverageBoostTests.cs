using System.Text.Json;
using DevTools.Execution;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Moq;
using Package = DevTools.Execution.Models.Package;

namespace DevTools.Execution.Tests;

public sealed class ExecutionCoverageBoostTests
{
    [Fact]
    public void PackageTreeNodes_BuildHierarchyAndRoundTrip()
    {
        var package = new Package(Marketplace.PyPi, "requests", "2.32.0", "2.32.0", true, "2.32.1", false);
        var item = new PackageItemNode(package);
        var conda = new MarketplaceNode(Marketplace.CondaForge);
        var nuget = new MarketplaceNode(Marketplace.NuGet);

        conda.Children.Add(item);
        Assert.Equal("requests (2.32.0)", item.Name);
        Assert.Equal("Conda-forge", conda.Name);
        Assert.Equal("NuGet", nuget.Name);
        Assert.True(item.IsProtected);
        Assert.False(item.IsLatest);

        var roundTrip = item.ToRuntimePackage();
        Assert.Equal(package.PackageId, roundTrip.PackageId);
        Assert.Equal(package.Marketplace, roundTrip.Marketplace);
    }

    [Fact]
    public async Task NullDocumentBridge_ReturnsUnavailableResults()
    {
        var open = await NullDocumentBridge.Instance.OpenDocumentAsync("missing.rvt", TestContext.Current.CancellationToken);
        var close = await NullDocumentBridge.Instance.CloseDocumentAsync(save: false, TestContext.Current.CancellationToken);
        var save = await NullDocumentBridge.Instance.SaveDocumentAsync(null, TestContext.Current.CancellationToken);

        Assert.False(open.Success);
        Assert.False(close.Success);
        Assert.False(save.Success);
        Assert.Contains("not available", open.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Collection(nameof(NugetRestoreCollection))]
    public sealed class NugetCompileBoostTests
    {
        [Fact]
        public async Task CSharpCompiler_WithNugetReference_ResolvesAndCompiles()
        {
            const string code = """
                                  #r "nuget: Newtonsoft.Json, 13.0.3"
                                  public sealed class ScriptCommand
                                  {
                                      public static int M() => 1;
                                  }
                                  """;

            var compiler = new CSharpCompiler(
                NullLogger<CSharpCompiler>.Instance,
                new NugetManager(NullLogger<NugetManager>.Instance));

            var result = await compiler.CompileAsync(code, ExecutionTestHelpers.CreateScriptBridge(), ct: TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.FormatDiagnostics());
            Assert.NotNull(result.Command);
            result.Cleanup?.Dispose();
        }
    }

    [Fact]
    public async Task FSharpExecutionStrategy_CompileFailure_ReturnsFailedResult()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-strategy-fail");
        var scriptPath = Path.Combine(directory, "bad_script.fsx");
        await File.WriteAllTextAsync(scriptPath, "let x =", TestContext.Current.CancellationToken);

        var bridge = ExecutionTestHelpers.CreateScriptBridge();
        var resolver = new FSharpDependencyResolver(NullLogger<FSharpDependencyResolver>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));
        var executor = new FSharpExecutor(NullLogger<FSharpExecutor>.Instance);
        var cache = new FSharpCompilationCache(bridge, resolver, executor, NullLogger<FSharpCompilationCache>.Instance);

        try
        {
            var strategy = new FSharpExecutionStrategy(
                scriptPath,
                ExecutionTestHelpers.InlineHostContext(),
                Mock.Of<ICommandRunner>(),
                cache,
                NullLogger<FSharpExecutionStrategy>.Instance);

            var result = await strategy.ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.Success);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Collection(nameof(PythonRuntimeCollection))]
    public sealed class PythonRuntimeBoostTests
    {
        [Fact]
        public async Task PipAndUvPackageStores_InvokeOperations_WhenProviderMismatch()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var pipStore = new PipPackageStore(initializer);
            var uvStore = new UvPackageStore(initializer);

            _ = await pipStore.ListAsync(TestContext.Current.CancellationToken);
            _ = await uvStore.ListAsync(TestContext.Current.CancellationToken);

            var package = new Package(Marketplace.PyPi, "six", null, null);
            await pipStore.RemoveAsync(package, TestContext.Current.CancellationToken);
            await pipStore.UpdateAsync(package, TestContext.Current.CancellationToken);
            await pipStore.RepairAsync(package, TestContext.Current.CancellationToken);
            await uvStore.RemoveAsync(package, TestContext.Current.CancellationToken);
            await uvStore.UpdateAsync(package, TestContext.Current.CancellationToken);
            await uvStore.RepairAsync(package, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task PixiPackageStore_List_ReturnsInstalledPackages()
        {
            await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var store = new PixiPackageStore(NullLogger<PixiPackageStore>.Instance);

            var packages = await store.ListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(packages);
        }

        [Fact]
        public async Task PackageService_WithPixiProvider_ListInstalledPackages_IncludesPythonSide()
        {
            PythonEmbedded.Configure(HostApp.Revit);
            using var provider = ExecutionTestHelpers.BuildExecutionServiceProvider();
            var initializer = provider.GetRequiredService<PythonInitializer>();
            await initializer.InitializeAsync();
            ExecutionTestHelpers.EnsureDevtoolNamespace(initializer);

            var service = provider.GetRequiredService<IPackageService>();
            var packages = await service.ListInstalledPackagesAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(packages);
        }

        [Fact]
        public async Task PythonExecutionStrategy_RunsSimpleScript()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var executor = new PythonExecutor(initializer);
            var directory = ExecutionTestHelpers.CreateTempDirectory("python-strategy-run");
            var scriptPath = Path.Combine(directory, "hello_script.py");
            await File.WriteAllTextAsync(scriptPath, "value = 40 + 2", TestContext.Current.CancellationToken);

            try
            {
                var strategy = new PythonExecutionStrategy(
                    scriptPath,
                    directory,
                    initializer,
                    executor,
                    ExecutionTestHelpers.InlineHostContext(),
                    NullLogger<PythonExecutionStrategy>.Instance);

                var result = await strategy.ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(result.Success, result.Message);
            }
            finally
            {
                TryDeleteDirectory(directory);
            }
        }

        [Fact]
        public async Task PythonCodeTool_ExecutesInlineCode_WhenPythonReady()
        {
            var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var tool = new PythonCodeTool(initializer, new PythonExecutor(initializer), ExecutionTestHelpers.InlineHostContext());

            var result = await InvokeToolAsync(tool, new { code = "print('coverage-boost')" });

            Assert.NotEqual(true, result.IsError);
        }

        [Fact]
        public async Task PixiPackageStore_UpdateAndRepair_ProtectedPackage_DoesNotThrow()
        {
            await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var store = new PixiPackageStore(NullLogger<PixiPackageStore>.Instance);
            var installed = await store.ListAsync(TestContext.Current.CancellationToken);
            var target = installed.FirstOrDefault(p => p.IsProtected) ?? new Package(Marketplace.PyPi, "pip", null, null, true);

            await store.UpdateAsync(target, TestContext.Current.CancellationToken);
            await store.RepairAsync(target, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task PixiEnvironmentProvider_InstallPackagesAsync_DoesNotThrow()
        {
            await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
            var provider = new PixiEnvironmentProvider(NullLogger<PixiEnvironmentProvider>.Instance);
            var messages = new List<string>();

            await provider.InstallPackagesAsync(["six"], new Progress<string>(messages.Add), TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task PackageService_UpdateLatestAsync_ForPythonPackage_DoesNotThrow()
        {
            PythonEmbedded.Configure(HostApp.Revit);
            using var sp = ExecutionTestHelpers.BuildExecutionServiceProvider();
            var initializer = sp.GetRequiredService<PythonInitializer>();
            await initializer.InitializeAsync();
            ExecutionTestHelpers.EnsureDevtoolNamespace(initializer);

            var service = sp.GetRequiredService<IPackageService>();
            await service.UpdateLatestAsync(new Package(Marketplace.PyPi, "six", null, null), TestContext.Current.CancellationToken);
            await service.RepairAsync(new Package(Marketplace.PyPi, "six", null, null), TestContext.Current.CancellationToken);
        }
    }

    private static async Task<CallToolResult> InvokeToolAsync(PythonCodeTool tool, object args)
    {
        var argumentMap = JsonSerializer.SerializeToElement(args).EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value);

        return await tool.ServerTool.InvokeAsync(
            new RequestContext<CallToolRequestParams>(
                Mock.Of<McpServer>(),
                new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
                new CallToolRequestParams
                {
                    Name = tool.Name,
                    Arguments = argumentMap,
                }),
            TestContext.Current.CancellationToken);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best effort
        }
    }
}
