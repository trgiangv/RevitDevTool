using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;
using DevTools.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Python.Runtime;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PythonInitializerTests
{
    [TestMethod]
    public async Task InitializeAsync_WithPixi_SetsProviderAndGlobalScope()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        await PixiInstaller.SetupPixiAsync(NullLogger.Instance);

        var pixi = new PixiEnvironmentProvider(NullLogger<PixiEnvironmentProvider>.Instance);
        if (!pixi.IsEnvironmentReady())
            await pixi.SetupEnvironmentAsync();

        var initializer = ExecutionTestHelpers.CreatePythonInitializer(pixi: pixi);

        await initializer.InitializeAsync();
        ExecutionTestHelpers.EnsureDevtoolNamespace(initializer);

        Assert.IsTrue(initializer.IsInitialized);
        Assert.IsNotNull(initializer.Provider);
        if (!initializer.HostOwnsInterpreter)
            Assert.AreEqual(PythonBackend.Pixi, initializer.Provider.Backend);
        Assert.IsNotNull(initializer.GlobalScope);
        Assert.IsTrue(PythonEngine.IsInitialized);
    }

    [TestMethod]
    public async Task InitializeAsync_SecondCall_IsIdempotent()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var provider = initializer.Provider;

        await initializer.InitializeAsync();

        Assert.AreSame(provider, initializer.Provider);
        Assert.IsTrue(initializer.IsInitialized);
    }

    [TestMethod]
    public async Task ShutdownAsync_WhenNotInitialized_DoesNotThrow()
    {
        var initializer = ExecutionTestHelpers.CreatePythonInitializer();
        await initializer.ShutdownAsync();
    }

    [TestMethod]
    public async Task InitializeAsync_SetsUpBuiltinsAndLogFunction()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();

        using (Py.GIL())
        {
            dynamic builtins = Py.Import("builtins");
            Assert.IsNotNull(builtins.__log_func__);
            Assert.IsNotNull(initializer.GlobalScope);
            initializer.GlobalScope!.Exec("import sys; assert sys.version_info >= (3, 11)");
        }
    }
}
