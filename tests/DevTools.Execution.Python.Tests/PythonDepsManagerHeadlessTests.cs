using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PythonDepsManagerHeadlessTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ResolveDependenciesAsync_InlineWithoutPep723_ReturnsEmpty()
    {
        var provider = new PixiEnvironmentProvider(Microsoft.Extensions.Logging.Abstractions.NullLogger<PixiEnvironmentProvider>.Instance);

        var deps = await PythonDepsManager.ResolveDependenciesAsync(
            provider,
            "print('hello')",
            TestContext.CancellationToken);

        Assert.IsEmpty(deps);
    }

    [TestMethod]
    public void RefreshImportCache_WhenNotInitialized_DoesNotThrow()
    {
        var initializer = new PythonInitializer(
            new PixiEnvironmentProvider(Microsoft.Extensions.Logging.Abstractions.NullLogger<PixiEnvironmentProvider>.Instance),
            new UvEnvironmentProvider(Microsoft.Extensions.Logging.Abstractions.NullLogger<UvEnvironmentProvider>.Instance),
            new PipEnvironmentProvider(Microsoft.Extensions.Logging.Abstractions.NullLogger<PipEnvironmentProvider>.Instance),
            ExecutionTestHelpers.CreatePythonBridge(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PythonInitializer>.Instance);

        PythonDepsManager.RefreshImportCache(initializer);
    }
}
