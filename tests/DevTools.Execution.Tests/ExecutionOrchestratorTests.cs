using DevTools.Execution.Abstractions;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Services;
using DevTools.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevTools.Execution.Tests;

public sealed class ExecutionOrchestratorTests
{
    [Fact]
    public async Task LoadFromPathAsync_NoProviderCanHandle_ThrowsArgumentException()
    {
        var provider = CreateProvider(canHandle: false);
        using var orchestrator = CreateOrchestrator([provider.Object]);
        var directory = CreateTempDirectory();

        try
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => orchestrator.LoadFromPathAsync(directory, TestContext.Current.CancellationToken));

            Assert.Contains("No suitable provider found", ex.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadFromPathAsync_PathUnderExistingRoot_SkipsReload()
    {
        var rootDirectory = CreateTempDirectory();
        var childDirectory = Path.Combine(rootDirectory, "child");
        Directory.CreateDirectory(childDirectory);

        try
        {
            var provider = CreateProvider(canHandle: true, validatePath: true);
            provider
                .Setup(p => p.DiscoverAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string path, CancellationToken _) =>
                [
                    CreateRootNode(path),
                ]);
            provider.Setup(p => p.GetWatchPatterns()).Returns(["*.csx"]);

            var fileWatcher = new Mock<IFileWatcherService>();
            using var orchestrator = CreateOrchestrator([provider.Object], fileWatcher: fileWatcher.Object);

            await orchestrator.LoadFromPathAsync(rootDirectory, TestContext.Current.CancellationToken);
            await orchestrator.LoadFromPathAsync(childDirectory, TestContext.Current.CancellationToken);

            provider.Verify(
                p => p.DiscoverAsync(rootDirectory, It.IsAny<CancellationToken>()),
                Times.Once);
            provider.Verify(
                p => p.DiscoverAsync(childDirectory, It.IsAny<CancellationToken>()),
                Times.Never);
            fileWatcher.Verify(
                fw => fw.Watch(rootDirectory, It.IsAny<IEnumerable<string>>()),
                Times.Once);
            fileWatcher.Verify(
                fw => fw.Watch(childDirectory, It.IsAny<IEnumerable<string>>()),
                Times.Never);
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadFromPathAsync_HappyPath_WatchesPathAndRaisesTreeChanged()
    {
        var rootDirectory = CreateTempDirectory();

        try
        {
            var provider = CreateProvider(canHandle: true, validatePath: true);
            var discoveredRoot = CreateRootNode(rootDirectory);
            provider
                .Setup(p => p.DiscoverAsync(rootDirectory, It.IsAny<CancellationToken>()))
                .ReturnsAsync([discoveredRoot]);
            provider.Setup(p => p.GetWatchPatterns()).Returns(["*.csx"]);

            var fileWatcher = new Mock<IFileWatcherService>();
            var treeChangedCount = 0;
            using var orchestrator = CreateOrchestrator([provider.Object], fileWatcher: fileWatcher.Object);
            orchestrator.TreeChanged += (_, _) => treeChangedCount++;

            await orchestrator.LoadFromPathAsync(rootDirectory, TestContext.Current.CancellationToken);

            Assert.Equal(1, treeChangedCount);
            fileWatcher.Verify(
                fw => fw.Watch(rootDirectory, It.Is<IEnumerable<string>>(patterns => patterns.Contains("*.csx"))),
                Times.Once);

            var root = Assert.IsType<ExecutionNodeRoot>(Assert.Single(orchestrator.TreeRoot));
            Assert.Equal(rootDirectory, root.RootPath);
        }
        finally
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    private static ExecutionOrchestrator CreateOrchestrator(
        IReadOnlyList<IExecutionProvider> providers,
        IFileWatcherService? fileWatcher = null,
        ITreeStateManager? stateManager = null)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(sp => sp.GetService(typeof(IEnumerable<IExecutionProvider>)))
            .Returns(providers);

        var state = stateManager ?? CreateStateManager();
        var watcher = fileWatcher ?? CreateFileWatcher();

        return new ExecutionOrchestrator(
            serviceProvider.Object,
            state,
            watcher,
            Mock.Of<ITelemetry>(),
            NullLogger<ExecutionOrchestrator>.Instance);
    }

    private static Mock<IExecutionProvider> CreateProvider(bool canHandle, bool validatePath = false)
    {
        var provider = new Mock<IExecutionProvider>();
        provider.SetupGet(p => p.Name).Returns("TestProvider");
        provider.SetupGet(p => p.Priority).Returns(0);
        provider.Setup(p => p.CanHandle(It.IsAny<string>())).Returns(canHandle);
        provider.Setup(p => p.ValidatePath(It.IsAny<string>())).Returns(validatePath);
        return provider;
    }

    private static ITreeStateManager CreateStateManager()
    {
        var stateManager = new Mock<ITreeStateManager>();
        stateManager
            .Setup(m => m.CaptureState(It.IsAny<IEnumerable<ExecutionNodeBase>>()))
            .Returns(new TreeState());
        stateManager
            .Setup(m => m.RestoreState(
                It.IsAny<IEnumerable<ExecutionNodeBase>>(),
                It.IsAny<TreeState>(),
                It.IsAny<bool>()));
        return stateManager.Object;
    }

    private static IFileWatcherService CreateFileWatcher()
    {
        var fileWatcher = new Mock<IFileWatcherService>();
        fileWatcher.Setup(fw => fw.Watch(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()));
        fileWatcher.Setup(fw => fw.Unwatch(It.IsAny<string>()));
        fileWatcher.Setup(fw => fw.UnwatchAll());
        return fileWatcher.Object;
    }

    private static ExecutionNodeRoot CreateRootNode(string rootPath) => new()
    {
        Id = $"root://{rootPath}",
        Name = Path.GetFileName(rootPath),
        RootPath = rootPath,
        ContainerMode = ContainerMode.Script,
        NodeType = NodeType.Container,
    };

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"execution-orchestrator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
