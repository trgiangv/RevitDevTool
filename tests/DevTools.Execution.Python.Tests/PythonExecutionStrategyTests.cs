using DevTools.Execution.Abstractions;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers;
using DevTools.Execution.Providers.Python;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PythonExecutionStrategyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ResolveDependenciesAsync_NoProvider_ReturnsTrue()
    {
        await using var provider = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var initializer = provider.GetRequiredService<PythonInitializer>();

        var ok = await PythonExecutionStrategy.ResolveDependenciesAsync(
            initializer,
            Path.Combine(ExecutionTestHelpers.CreateTempDirectory("python-deps"), "x.py"),
            cancellationToken: TestContext.CancellationToken);

        Assert.IsTrue(ok);
    }
}
