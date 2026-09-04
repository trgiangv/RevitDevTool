using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;
using DevTools.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Python.Runtime;

namespace DevTools.Execution.Tests;

[Collection(nameof(PythonRuntimeCollection))]
public sealed class PythonInitializerTests
{
    [Fact]
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

        Assert.True(initializer.IsInitialized);
        Assert.NotNull(initializer.Provider);
        if (!initializer.HostOwnsInterpreter)
            Assert.Equal(PythonBackend.Pixi, initializer.Provider.Backend);
        Assert.NotNull(initializer.GlobalScope);
        Assert.True(PythonEngine.IsInitialized);
    }

    [Fact]
    public async Task InitializeAsync_SecondCall_IsIdempotent()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var provider = initializer.Provider;

        await initializer.InitializeAsync();

        Assert.Same(provider, initializer.Provider);
        Assert.True(initializer.IsInitialized);
    }

    [Fact]
    public async Task ShutdownAsync_WhenNotInitialized_DoesNotThrow()
    {
        var initializer = ExecutionTestHelpers.CreatePythonInitializer();
        await initializer.ShutdownAsync();
    }

    [Fact]
    public async Task InitializeAsync_SetsUpBuiltinsAndLogFunction()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();

        using (Py.GIL())
        {
            dynamic builtins = Py.Import("builtins");
            Assert.NotNull(builtins.__log_func__);
            Assert.NotNull(initializer.GlobalScope);
            initializer.GlobalScope!.Exec("import sys; assert sys.version_info >= (3, 11)");
        }
    }
}
