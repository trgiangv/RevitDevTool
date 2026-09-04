using DevTools.Execution.Abstractions;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Services;
using DevTools.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevTools.Execution.Tests;

public sealed class ExecutionOrchestratorExtendedTests
{
    [Fact]
    public async Task ExecuteAsync_ExecutableNode_RecordsProgressAndTelemetry()
    {
        var strategy = new Mock<IExecutionStrategy>();
        strategy
            .Setup(s => s.ExecuteAsync(It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .Returns<IProgress<string>?, CancellationToken>((progress, _) =>
            {
                progress?.Report("running");
                return Task.FromResult(ExecutionResult.Succeeded("done", 10));
            });

        var node = ExecutionTestHelpers.CreateExecutableNode("exec://run", strategy.Object);
        var telemetry = new Mock<ITelemetry>();
        using var orchestrator = CreateOrchestrator(telemetry: telemetry.Object);

        var progressMessages = new List<string>();
        orchestrator.ExecutionProgressChanged += (_, args) => progressMessages.Add(args.Message);

        var result = await orchestrator.ExecuteAsync(node, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.True(node.IsLastExecuted);
        Assert.Contains("done", progressMessages);
        telemetry.Verify(t => t.RecordExecutionInvocation("CSharp", true), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NonExecutable_ReturnsSkipped()
    {
        var container = new ExecutionNodeIntermediate
        {
            Id = "container://x",
            Name = "x",
            FullPath = "x",
            NodeType = NodeType.Container,
        };

        using var orchestrator = CreateOrchestrator();
        var result = await orchestrator.ExecuteAsync(container, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("Node is not executable.", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_FailedResult_RecordsFailedTelemetry()
    {
        var strategy = new Mock<IExecutionStrategy>();
        strategy
            .Setup(s => s.ExecuteAsync(It.IsAny<IProgress<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExecutionResult.Failed("boom"));

        var telemetry = new Mock<ITelemetry>();
        var node = ExecutionTestHelpers.CreateExecutableNode("exec://fail", strategy.Object);
        using var orchestrator = CreateOrchestrator(telemetry: telemetry.Object);

        var result = await orchestrator.ExecuteAsync(node, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        telemetry.Verify(t => t.RecordExecutionInvocation("CSharp", false), Times.Once);
    }

    [Fact]
    public async Task ReloadAsync_RefreshesDiscoveredRoots()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("orchestrator-reload");
        var provider = new Mock<IExecutionProvider>();
        provider.SetupGet(p => p.Name).Returns("Script");
        provider.SetupGet(p => p.Priority).Returns(100);
        provider.Setup(p => p.CanHandle(directory)).Returns(true);
        provider.Setup(p => p.ValidatePath(directory)).Returns(true);
        provider.Setup(p => p.GetWatchPatterns()).Returns(["*script.csx"]);
        provider
            .SetupSequence(p => p.DiscoverAsync(directory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ExecutionTestHelpers.CreateScriptRoot(directory, ExecutionTestHelpers.CreateExecutableNode("exec://first"))])
            .ReturnsAsync([ExecutionTestHelpers.CreateScriptRoot(directory, ExecutionTestHelpers.CreateExecutableNode("exec://second"))]);

        var fileWatcher = CreateFileWatcher();
        using var orchestrator = CreateOrchestrator(
            providers: [provider.Object],
            keyedProviders: [(ContainerMode.Script, provider.Object)],
            fileWatcher: fileWatcher.Object);

        try
        {
            await orchestrator.LoadFromPathAsync(directory, TestContext.Current.CancellationToken);
            await orchestrator.ReloadAsync(TestContext.Current.CancellationToken);

            var root = Assert.IsType<ExecutionNodeRoot>(Assert.Single(orchestrator.TreeRoot));
            Assert.Equal("exec://second", Assert.IsType<ExecutionNode>(Assert.Single(root.Children)).Id);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadSavedPathsAsync_InvalidPath_ReturnsFailedEntry()
    {
        using var orchestrator = CreateOrchestrator();
        var failed = await orchestrator.LoadSavedPathsAsync(["   ", "missing-path"], TestContext.Current.CancellationToken);

        Assert.Equal(2, failed.Count);
    }

    [Fact]
    public async Task RemoveNodeAndClearAll_UpdateTree()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("orchestrator-remove");
        var provider = new Mock<IExecutionProvider>();
        provider.SetupGet(p => p.Priority).Returns(100);
        provider.Setup(p => p.CanHandle(directory)).Returns(true);
        provider.Setup(p => p.ValidatePath(directory)).Returns(true);
        provider.Setup(p => p.GetWatchPatterns()).Returns(["*script.csx"]);
        provider
            .Setup(p => p.DiscoverAsync(directory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ExecutionTestHelpers.CreateScriptRoot(directory, ExecutionTestHelpers.CreateExecutableNode("exec://one"))]);

        var fileWatcher = CreateFileWatcher();
        using var orchestrator = CreateOrchestrator(
            providers: [provider.Object],
            fileWatcher: fileWatcher.Object);

        await orchestrator.LoadFromPathAsync(directory, TestContext.Current.CancellationToken);
        var loadedRoot = Assert.IsType<ExecutionNodeRoot>(Assert.Single(orchestrator.TreeRoot));
        var next = orchestrator.RemoveNode(loadedRoot);

        Assert.Null(next);
        Assert.Empty(orchestrator.TreeRoot);
        fileWatcher.Verify(w => w.Unwatch(directory), Times.AtLeastOnce);

        orchestrator.ClearAll();
        fileWatcher.Verify(w => w.UnwatchAll(), Times.AtLeastOnce);

        Directory.Delete(directory, recursive: true);
    }

    private static ExecutionOrchestrator CreateOrchestrator(
        IReadOnlyList<IExecutionProvider>? providers = null,
        IReadOnlyList<(ContainerMode Mode, IExecutionProvider Provider)>? keyedProviders = null,
        IFileWatcherService? fileWatcher = null,
        ITelemetry? telemetry = null)
    {
        var services = new ServiceCollection();
        foreach (var provider in providers ?? [])
            services.AddSingleton(provider);

        foreach (var (mode, provider) in keyedProviders ?? [])
            services.AddKeyedSingleton(mode, provider);

        services.AddSingleton<ITreeStateManager, TreeStateManager>();
        services.AddSingleton(fileWatcher ?? CreateFileWatcher().Object);
        services.AddSingleton(telemetry ?? Mock.Of<ITelemetry>());

        var built = services.BuildServiceProvider();
        return new ExecutionOrchestrator(
            built,
            built.GetRequiredService<ITreeStateManager>(),
            built.GetRequiredService<IFileWatcherService>(),
            built.GetRequiredService<ITelemetry>(),
            NullLogger<ExecutionOrchestrator>.Instance);
    }

    private static Mock<IFileWatcherService> CreateFileWatcher()
    {
        var watcher = new Mock<IFileWatcherService>();
        watcher.Setup(w => w.Watch(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()));
        watcher.Setup(w => w.Unwatch(It.IsAny<string>()));
        watcher.Setup(w => w.UnwatchAll());
        return watcher;
    }
}
