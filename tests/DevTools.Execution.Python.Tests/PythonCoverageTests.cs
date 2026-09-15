using System.Text.Json;
using DevTools.Execution;
using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.BuiltIn;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
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

[TestClass]
public sealed class PythonCoverageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void PythonEmbedded_AutoCadSetup_SelectsAcadScript()
    {
        PythonEmbedded.Configure(HostApp.AutoCad);
        PythonEmbedded.EnsureExtracted();

        Assert.AreEqual("SetupAcad.py", PythonEmbedded.SetupScriptFileName);
        Assert.IsFalse(string.IsNullOrWhiteSpace(PythonEmbedded.SetupScript));
        Assert.IsFalse(string.IsNullOrWhiteSpace(PythonEmbedded.ParserScriptPath));
    }

    [TestMethod]
    public void PythonEmbedded_RevitSetup_ExposesEmbeddedScripts()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        PythonEmbedded.EnsureExtracted();

        Assert.AreEqual("SetupRevit.py", PythonEmbedded.SetupScriptFileName);
        Assert.IsFalse(string.IsNullOrWhiteSpace(PythonEmbedded.ToolParserScript));
        Assert.IsFalse(string.IsNullOrWhiteSpace(PythonEmbedded.PytestRunnerScript));
        Assert.IsFalse(string.IsNullOrWhiteSpace(PythonEmbedded.IpyDebuggerScript));
    }

    [TestMethod]
    public void PythonEmbedded_DoubleEnsureExtracted_IsIdempotent()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        PythonEmbedded.EnsureExtracted();
        PythonEmbedded.EnsureExtracted();
        Assert.IsFalse(string.IsNullOrWhiteSpace(PythonEmbedded.ResetScript));
    }

    [TestMethod]
    public void PythonDepsManager_TryResolveSidecarStdlib_FromLibDir()
    {
        var root = ExecutionTestHelpers.CreateTempDirectory("stdlib-sidecar");
        var lib = Path.Combine(root, "Lib");
        var dlls = Path.Combine(root, "DLLs");
        Directory.CreateDirectory(lib);
        Directory.CreateDirectory(dlls);

        try
        {
            Assert.IsTrue(PythonDepsManager.TryResolveSidecarStdlib(lib, null, out var stdlibLib, out var stdlibDlls));
            Assert.AreEqual(lib, stdlibLib);
            Assert.AreEqual(dlls, stdlibDlls);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PipEnvironmentProvider_SelectCengineDir_ReturnsNull_WhenVersionMismatch()
    {
        var root = ExecutionTestHelpers.CreateTempDirectory("pip-version");
        var engine = Path.Combine(root, "CPY_3_13");
        Directory.CreateDirectory(engine);
        File.WriteAllText(Path.Combine(engine, "python.exe"), string.Empty);
        File.WriteAllBytes(Path.Combine(engine, "python313.dll"), [1]);

        try
        {
            Assert.IsNull(PipEnvironmentProvider.SelectCengineDir([engine], "9.99"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ResolveDependenciesAsync_WithPep723Inline_Completes()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var provider = initializer.Provider!;
        Assert.IsTrue(provider.IsEnvironmentReady());

        const string script = """
            # /// script
            # dependencies = ["six"]
            print("deps")
            """;

        var deps = await PythonDepsManager.ResolveDependenciesAsync(provider, script, TestContext.CancellationToken);

        Assert.IsNotNull(deps);
    }

    [TestMethod]
    public async Task InstallDependenciesAsync_EmptyList_IsNoOp()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var provider = initializer.Provider!;
        var messages = new List<string>();

        await PythonDepsManager.InstallDependenciesAsync(
            provider,
            [],
            new Progress<string>(messages.Add),
            TestContext.CancellationToken);

        Assert.IsEmpty(messages);
    }

    [TestMethod]
    public async Task RefreshImportCache_WhenInitialized_DoesNotThrow()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        PythonDepsManager.RefreshImportCache(initializer);
    }

    [TestMethod]
    public async Task InstallDependenciesAsync_WithPackage_ReportsProgress()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var provider = initializer.Provider!;
        var messages = new List<string>();

        await PythonDepsManager.InstallDependenciesAsync(
            provider,
            ["six"],
            new Progress<string>(messages.Add),
            TestContext.CancellationToken);

        Assert.IsNotEmpty(messages);
    }

    [TestMethod]
    public async Task PixiEnvironmentProvider_InstallPackagesAsync_NumpyPartition_DoesNotThrow()
    {
        await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var provider = new PixiEnvironmentProvider(NullLogger<PixiEnvironmentProvider>.Instance);
        var messages = new List<string>();

        await provider.InstallPackagesAsync(["numpy"], new Progress<string>(messages.Add), TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task PipAndUvPackageStores_InvokeOperations_WhenProviderMismatch()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var pipStore = new PipPackageStore(initializer);
        var uvStore = new UvPackageStore(initializer);

        _ = await pipStore.ListAsync(TestContext.CancellationToken);
        _ = await uvStore.ListAsync(TestContext.CancellationToken);

        var package = new Package(Marketplace.PyPi, "six", null, null);
        await pipStore.RemoveAsync(package, TestContext.CancellationToken);
        await pipStore.UpdateAsync(package, TestContext.CancellationToken);
        await pipStore.RepairAsync(package, TestContext.CancellationToken);
        await uvStore.RemoveAsync(package, TestContext.CancellationToken);
        await uvStore.UpdateAsync(package, TestContext.CancellationToken);
        await uvStore.RepairAsync(package, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task PixiPackageStore_List_ReturnsInstalledPackages()
    {
        await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var store = new PixiPackageStore(NullLogger<PixiPackageStore>.Instance);

        var packages = await store.ListAsync(TestContext.CancellationToken);

        Assert.IsNotNull(packages);
    }

    [TestMethod]
    public async Task PythonExecutionStrategy_RunsSimpleScript()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var directory = ExecutionTestHelpers.CreateTempDirectory("python-strategy-run");
        var scriptPath = Path.Combine(directory, "hello_script.py");
        await File.WriteAllTextAsync(scriptPath, "value = 40 + 2", TestContext.CancellationToken);

        try
        {
            var strategy = new PythonExecutionStrategy(
                scriptPath,
                directory,
                initializer,
                ExecutionTestHelpers.InlineHostContext(),
                NullLogger<PythonExecutionStrategy>.Instance);

            var result = await strategy.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

            Assert.IsTrue(result.Success, result.Message);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    [TestMethod]
    public async Task PythonCodeTool_ExecutesInlineCode_WhenPythonReady()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var tool = new PythonCodeTool(initializer, ExecutionTestHelpers.InlineHostContext());

        var result = await InvokeToolAsync(tool, new { code = "print('coverage-boost')" }, TestContext.CancellationToken);

        Assert.AreNotEqual(true, result.IsError);
    }

    [TestMethod]
    public async Task PixiPackageStore_UpdateAndRepair_ProtectedPackage_DoesNotThrow()
    {
        await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var store = new PixiPackageStore(NullLogger<PixiPackageStore>.Instance);
        var installed = await store.ListAsync(TestContext.CancellationToken);
        var target = installed.FirstOrDefault(p => p.IsProtected) ?? new Package(Marketplace.PyPi, "pip", null, null, true);

        await store.UpdateAsync(target, TestContext.CancellationToken);
        await store.RepairAsync(target, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task PixiEnvironmentProvider_InstallPackagesAsync_DoesNotThrow()
    {
        await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var provider = new PixiEnvironmentProvider(NullLogger<PixiEnvironmentProvider>.Instance);
        var messages = new List<string>();

        await provider.InstallPackagesAsync(["six"], new Progress<string>(messages.Add), TestContext.CancellationToken);
    }

    private static async Task<CallToolResult> InvokeToolAsync(PythonCodeTool tool, object args, CancellationToken cancellationToken)
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
            cancellationToken);
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
