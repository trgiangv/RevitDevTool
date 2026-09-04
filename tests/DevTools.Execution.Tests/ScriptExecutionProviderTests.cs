using DevTools.Execution.Abstractions;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevTools.Execution.Tests;

public sealed class ScriptExecutionProviderTests
{
    [Fact]
    public async Task DiscoverAsync_FindsCsxPyAndFsxScripts()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("script-provider");
        await File.WriteAllTextAsync(Path.Combine(directory, "alpha_script.csx"), "// csx", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "beta_script.py"), "print(1)", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "gamma_ipy_script.py"), "print(2)", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "delta_script.fsx"), "// fsx", TestContext.Current.CancellationToken);

        var factory = new Mock<IScriptExecutionStrategyFactory>();
        factory
            .Setup(f => f.Create(It.IsAny<ExecutionMode>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns<ExecutionMode, string, string>((mode, path, _) => new StubStrategy(mode, path));

        var provider = new ScriptExecutionProvider(factory.Object, NullLogger<ScriptExecutionProvider>.Instance);

        try
        {
            var nodes = await provider.DiscoverAsync(directory, TestContext.Current.CancellationToken);
            var root = Assert.IsType<ExecutionNodeRoot>(Assert.Single(nodes));
            Assert.Equal(4, root.Children.Count);

            var modes = root.Children.Cast<ExecutionNode>().Select(node => node.ExecutionMode).OrderBy(mode => mode.ToString()).ToArray();
            Assert.Equal(ExecutionMode.CSharp, modes[0]);
            Assert.Equal(ExecutionMode.FSharp, modes[1]);
            Assert.Equal(ExecutionMode.IronPython, modes[2]);
            Assert.Equal(ExecutionMode.Python, modes[3]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DiscoverAsync_NoEntryScripts_ReturnsEmpty()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("script-provider-empty");
        var provider = new ScriptExecutionProvider(Mock.Of<IScriptExecutionStrategyFactory>(), NullLogger<ScriptExecutionProvider>.Instance);

        try
        {
            var nodes = await provider.DiscoverAsync(directory, TestContext.Current.CancellationToken);
            Assert.Empty(nodes);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DiscoverAsync_SkipsIgnoredSubfolders()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("script-provider-skip");
        var nodeModules = Path.Combine(directory, "node_modules");
        Directory.CreateDirectory(nodeModules);
        await File.WriteAllTextAsync(Path.Combine(nodeModules, "hidden_script.py"), "print(1)", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "visible_script.py"), "print(2)", TestContext.Current.CancellationToken);

        var provider = new ScriptExecutionProvider(Mock.Of<IScriptExecutionStrategyFactory>(), NullLogger<ScriptExecutionProvider>.Instance);

        try
        {
            var nodes = await provider.DiscoverAsync(directory, TestContext.Current.CancellationToken);
            var root = Assert.IsType<ExecutionNodeRoot>(Assert.Single(nodes));
            Assert.Single(root.Children);
            Assert.Equal(ExecutionMode.Python, ((ExecutionNode)root.Children[0]).ExecutionMode);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void CanHandle_AndValidatePath_AcceptDirectoriesOnly()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("script-provider-handle");
        var provider = new ScriptExecutionProvider(Mock.Of<IScriptExecutionStrategyFactory>(), NullLogger<ScriptExecutionProvider>.Instance);

        try
        {
            Assert.True(provider.CanHandle(directory));
            Assert.True(provider.ValidatePath(directory));
            Assert.False(provider.CanHandle(Path.Combine(directory, "missing.csx")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class StubStrategy(ExecutionMode mode, string path) : IExecutionStrategy
    {
        public Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExecutionResult.Succeeded($"{mode}:{path}", 0));
    }
}
