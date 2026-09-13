using DevTools.Execution.Abstractions;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevTools.Execution.Tests;

[TestClass]

public sealed class ScriptExecutionProviderExtendedTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DiscoverAsync_MissingDirectory_ReturnsEmpty()
    {
        var provider = new ScriptExecutionProvider(
            Mock.Of<IScriptExecutionStrategyFactory>(),
            NullLogger<ScriptExecutionProvider>.Instance);

        var nodes = await provider.DiscoverAsync(
            Path.Combine(Path.GetTempPath(), $"missing-script-root-{Guid.NewGuid():N}"),
            TestContext.CancellationToken);

        Assert.IsEmpty(nodes);
    }

    [TestMethod]
    public async Task DiscoverAsync_NestedSubfolder_BuildsIntermediateNode()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("script-provider-nested");
        var tools = Path.Combine(directory, "tools");
        Directory.CreateDirectory(tools);
        await File.WriteAllTextAsync(Path.Combine(directory, "root_script.py"), "print(1)", TestContext.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(tools, "tool_script.py"), "print(2)", TestContext.CancellationToken);

        var factory = new Mock<IScriptExecutionStrategyFactory>();
        factory
            .Setup(f => f.Create(It.IsAny<ExecutionMode>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new StubStrategy());

        var provider = new ScriptExecutionProvider(factory.Object, NullLogger<ScriptExecutionProvider>.Instance);

        try
        {
            var nodes = await provider.DiscoverAsync(directory, TestContext.CancellationToken);
            var root = Assert.IsExactInstanceOfType<ExecutionNodeRoot>(Assert.ContainsSingle(nodes));
            Assert.AreEqual(2, root.Children.Count);
            Assert.IsTrue(root.Children.Any(node => node is ExecutionNodeIntermediate));
            Assert.IsTrue(root.Children.Any(node => node is ExecutionNode));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task DiscoverAsync_EmptySubfolder_IsOmitted()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("script-provider-empty-sub");
        Directory.CreateDirectory(Path.Combine(directory, "empty"));
        await File.WriteAllTextAsync(Path.Combine(directory, "only_script.py"), "print(1)", TestContext.CancellationToken);

        var provider = new ScriptExecutionProvider(Mock.Of<IScriptExecutionStrategyFactory>(), NullLogger<ScriptExecutionProvider>.Instance);

        try
        {
            var nodes = await provider.DiscoverAsync(directory, TestContext.CancellationToken);
            var root = Assert.IsExactInstanceOfType<ExecutionNodeRoot>(Assert.ContainsSingle(nodes));
            Assert.ContainsSingle(root.Children);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Priority_IsNegativeHundred()
    {
        var provider = new ScriptExecutionProvider(Mock.Of<IScriptExecutionStrategyFactory>(), NullLogger<ScriptExecutionProvider>.Instance);
        Assert.AreEqual(-100, provider.Priority);
        Assert.AreEqual("Script", provider.Name);
    }

    [TestMethod]
    public void GetWatchPatterns_ReturnsAllScriptSuffixes()
    {
        var provider = new ScriptExecutionProvider(Mock.Of<IScriptExecutionStrategyFactory>(), NullLogger<ScriptExecutionProvider>.Instance);
        var patterns = provider.GetWatchPatterns().ToArray();

        Assert.AreEqual(3, patterns.Length);
        Assert.Contains("*script.py", patterns);
        Assert.Contains("*script.fsx", patterns);
        Assert.Contains("*script.csx", patterns);
    }

    private sealed class StubStrategy : IExecutionStrategy
    {
        public Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExecutionResult.Succeeded("ok", 0));
    }
}
