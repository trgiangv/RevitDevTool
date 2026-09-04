using DevTools.Execution.Abstractions;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Services;
using DevTools.Telemetry;
using DevTools.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevTools.Execution.Tests;

public sealed class ExecutionOrchestratorFileChangeTests
{
    [Fact]
    public async Task FileChanged_ModifiedEvent_IsIgnored()
    {
        if (HostUiHelper.HostDispatcher is not null)
            Assert.Skip("Host dispatcher is initialized; file-change handler requires inline dispatch.");

        var directory = ExecutionTestHelpers.CreateTempDirectory("orchestrator-modified");
        var provider = CreateProvider(directory);
        var fileWatcher = ExecutionTestHelpers.CreateFileWatcher();
        var treeChanged = 0;
        using var orchestrator = CreateOrchestrator([provider.Object], fileWatcher, ContainerMode.Script, provider.Object);
        orchestrator.TreeChanged += (_, _) => Interlocked.Increment(ref treeChanged);

        try
        {
            await orchestrator.LoadFromPathAsync(directory, TestContext.Current.CancellationToken);
            fileWatcher.Raise(new FileChangedEventArgs
            {
                Path = Path.Combine(directory, "changed.csx"),
                ChangeType = FileChangeType.Modified,
                Scope = FileWatcherScope.FileContent,
            });

            await Task.Delay(300, TestContext.Current.CancellationToken);
            Assert.Equal(1, treeChanged);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FileChanged_RootDeleted_RemovesRootAndUnwatches()
    {
        if (HostUiHelper.HostDispatcher is not null)
            Assert.Skip("Host dispatcher is initialized; file-change handler requires inline dispatch.");

        var directory = ExecutionTestHelpers.CreateTempDirectory("orchestrator-delete");
        var provider = CreateProvider(directory);
        var fileWatcher = ExecutionTestHelpers.CreateFileWatcher();
        string? removedPath = null;
        using var orchestrator = CreateOrchestrator([provider.Object], fileWatcher, ContainerMode.Script, provider.Object);
        orchestrator.RootRemoved += (_, args) => removedPath = args.RootPath;

        try
        {
            await orchestrator.LoadFromPathAsync(directory, TestContext.Current.CancellationToken);
            fileWatcher.Raise(new FileChangedEventArgs
            {
                Path = directory,
                ChangeType = FileChangeType.Deleted,
                Scope = FileWatcherScope.RootLifecycle,
            });

            await WaitForAsync(() => removedPath is not null, TestContext.Current.CancellationToken);

            Assert.Equal(directory, removedPath);
            Assert.Empty(orchestrator.TreeRoot);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FileChanged_DirectoryStructure_ReloadsAffectedRoot()
    {
        if (HostUiHelper.HostDispatcher is not null)
            Assert.Skip("Host dispatcher is initialized; file-change handler requires inline dispatch.");

        var directory = ExecutionTestHelpers.CreateTempDirectory("orchestrator-reload");
        var provider = CreateProvider(directory);
        provider
            .SetupSequence(p => p.DiscoverAsync(directory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ExecutionTestHelpers.CreateScriptRoot(directory, ExecutionTestHelpers.CreateExecutableNode("exec://first"))])
            .ReturnsAsync([ExecutionTestHelpers.CreateScriptRoot(directory, ExecutionTestHelpers.CreateExecutableNode("exec://second"))]);

        var fileWatcher = ExecutionTestHelpers.CreateFileWatcher();
        var treeChanged = 0;
        using var orchestrator = CreateOrchestrator([provider.Object], fileWatcher, ContainerMode.Script, provider.Object);
        orchestrator.TreeChanged += (_, _) => Interlocked.Increment(ref treeChanged);

        try
        {
            await orchestrator.LoadFromPathAsync(directory, TestContext.Current.CancellationToken);
            fileWatcher.Raise(new FileChangedEventArgs
            {
                Path = Path.Combine(directory, "nested"),
                ChangeType = FileChangeType.Created,
                Scope = FileWatcherScope.DirectoryStructure,
            });

            await WaitForAsync(() => treeChanged >= 2, TestContext.Current.CancellationToken);

            var root = Assert.IsType<ExecutionNodeRoot>(Assert.Single(orchestrator.TreeRoot));
            Assert.Equal("exec://second", Assert.IsType<ExecutionNode>(Assert.Single(root.Children)).Id);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FileChanged_RootRenamed_ReloadsAtNewPath()
    {
        if (HostUiHelper.HostDispatcher is not null)
            Assert.Skip("Host dispatcher is initialized; file-change handler requires inline dispatch.");

        var oldDirectory = ExecutionTestHelpers.CreateTempDirectory("orchestrator-old");
        var newDirectory = ExecutionTestHelpers.CreateTempDirectory("orchestrator-new");
        var provider = CreateProvider(oldDirectory, alsoHandles: newDirectory);
        provider
            .Setup(p => p.ValidatePath(newDirectory))
            .Returns(true);
        provider
            .Setup(p => p.DiscoverAsync(newDirectory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ExecutionTestHelpers.CreateScriptRoot(newDirectory, ExecutionTestHelpers.CreateExecutableNode("exec://renamed"))]);

        var fileWatcher = ExecutionTestHelpers.CreateFileWatcher();
        RootRemovedEventArgs? removed = null;
        using var orchestrator = CreateOrchestrator([provider.Object], fileWatcher, ContainerMode.Script, provider.Object);
        orchestrator.RootRemoved += (_, args) => removed = args;

        try
        {
            await orchestrator.LoadFromPathAsync(oldDirectory, TestContext.Current.CancellationToken);
            fileWatcher.Raise(new FileChangedEventArgs
            {
                Path = newDirectory,
                OldPath = oldDirectory,
                ChangeType = FileChangeType.Renamed,
                Scope = FileWatcherScope.RootLifecycle,
            });

            await WaitForAsync(() => removed is not null, TestContext.Current.CancellationToken);

            Assert.Equal(oldDirectory, removed!.RootPath);
            Assert.Equal(newDirectory, removed.NewPath);
            var root = Assert.IsType<ExecutionNodeRoot>(Assert.Single(orchestrator.TreeRoot));
            Assert.Equal(newDirectory, root.RootPath);
        }
        finally
        {
            if (Directory.Exists(oldDirectory))
                Directory.Delete(oldDirectory, recursive: true);
            if (Directory.Exists(newDirectory))
                Directory.Delete(newDirectory, recursive: true);
        }
    }

    private static Mock<IExecutionProvider> CreateProvider(string directory, string? alsoHandles = null)
    {
        var provider = new Mock<IExecutionProvider>();
        provider.SetupGet(p => p.Name).Returns("Script");
        provider.SetupGet(p => p.Priority).Returns(100);
        provider.Setup(p => p.CanHandle(It.IsAny<string>()))
            .Returns<string>(path =>
                Path.GetFullPath(path).Equals(Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase)
                || (alsoHandles is not null && Path.GetFullPath(path).Equals(Path.GetFullPath(alsoHandles), StringComparison.OrdinalIgnoreCase)));
        provider.Setup(p => p.ValidatePath(It.IsAny<string>())).Returns(true);
        provider.Setup(p => p.GetWatchPatterns()).Returns(["*.csx"]);
        provider
            .Setup(p => p.DiscoverAsync(directory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([ExecutionTestHelpers.CreateScriptRoot(directory, ExecutionTestHelpers.CreateExecutableNode("exec://initial"))]);
        return provider;
    }

    private static ExecutionOrchestrator CreateOrchestrator(
        IReadOnlyList<IExecutionProvider> providers,
        ExecutionTestHelpers.FakeFileWatcherService fileWatcher,
        ContainerMode mode,
        IExecutionProvider keyedProvider)
    {
        var services = new ServiceCollection();
        foreach (var provider in providers)
            services.AddSingleton(provider);
        services.AddKeyedSingleton(mode, keyedProvider);
        services.AddSingleton<ITreeStateManager, TreeStateManager>();
        services.AddSingleton<IFileWatcherService>(fileWatcher);
        services.AddSingleton(Mock.Of<ITelemetry>());

        var built = services.BuildServiceProvider();
        return new ExecutionOrchestrator(
            built,
            built.GetRequiredService<ITreeStateManager>(),
            built.GetRequiredService<IFileWatcherService>(),
            built.GetRequiredService<ITelemetry>(),
            NullLogger<ExecutionOrchestrator>.Instance);
    }

    private static async Task WaitForAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (condition())
                return;

            await Task.Delay(100, cancellationToken);
        }

        throw new TimeoutException("Timed out waiting for orchestrator file-change handling.");
    }
}
