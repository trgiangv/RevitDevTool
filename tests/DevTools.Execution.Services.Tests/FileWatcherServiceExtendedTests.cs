using DevTools.Execution.Abstractions;
using DevTools.Execution.Services;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class FileWatcherServiceExtendedTests
{
    public TestContext TestContext { get; set; } = null!;

    private static readonly TimeSpan DebounceWait = TimeSpan.FromMilliseconds(1000);

    [TestMethod]
    public async Task Watch_SubdirectoryCreation_RaisesDirectoryStructureEvent()
    {
        var root = CreateTempRoot();
        using var service = new FileWatcherService();
        var events = new List<FileChangedEventArgs>();
        service.FileChanged += (_, e) => events.Add(e);

        try
        {
            service.Watch(root, ["*.csx"]);
            var nested = Path.Combine(root, $"nested-{Guid.NewGuid():N}");
            Directory.CreateDirectory(nested);
            await WaitForDebounceAsync();

            Assert.IsTrue(events.Any(e => e.Scope == FileWatcherScope.DirectoryStructure));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [TestMethod]
    public async Task Watch_RootRename_RaisesRootLifecycleEvent()
    {
        var parent = CreateTempRoot();
        var root = Path.Combine(parent, $"root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        using var service = new FileWatcherService();
        var events = new List<FileChangedEventArgs>();
        service.FileChanged += (_, e) => events.Add(e);

        try
        {
            service.Watch(root, ["*.py"]);
            var renamed = Path.Combine(parent, $"renamed-{Guid.NewGuid():N}");
            Directory.Move(root, renamed);
            await Task.Delay(200, TestContext.CancellationToken);

            Assert.IsTrue(events.Any(e => e.Scope == FileWatcherScope.RootLifecycle && e.ChangeType == FileChangeType.Renamed));
        }
        finally
        {
            DeleteTempRoot(parent);
        }
    }

    [TestMethod]
    public async Task Watch_FileDelete_RaisesContentEvent()
    {
        var root = CreateTempRoot();
        using var service = new FileWatcherService();
        var events = new List<FileChangedEventArgs>();
        service.FileChanged += (_, e) => events.Add(e);

        try
        {
            service.Watch(root, ["*.csx"]);
            var scriptPath = Path.Combine(root, $"delete-me-{Guid.NewGuid():N}.csx");
            await File.WriteAllTextAsync(scriptPath, "// temp", TestContext.CancellationToken);
            await WaitForDebounceAsync();
            File.Delete(scriptPath);
            await WaitForDebounceAsync();

            Assert.IsTrue(events.Any(e => e.Scope == FileWatcherScope.FileContent && e.ChangeType == FileChangeType.Deleted));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [TestMethod]
    public async Task Watch_MultiplePatterns_WatchesBothExtensions()
    {
        var root = CreateTempRoot();
        using var service = new FileWatcherService();
        var events = new List<FileChangedEventArgs>();
        service.FileChanged += (_, e) => events.Add(e);

        try
        {
            service.Watch(root, ["*.csx", "*.py"]);
            var pyPath = Path.Combine(root, $"sample-{Guid.NewGuid():N}.py");
            await File.WriteAllTextAsync(pyPath, "x=1", TestContext.CancellationToken);
            await WaitForDebounceAsync();

            Assert.IsTrue(events.Any(e => e.Path.EndsWith(".py", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [TestMethod]
    public async Task Watch_FileRename_RaisesContentRenamedEvent()
    {
        var root = CreateTempRoot();
        using var service = new FileWatcherService();
        var events = new List<FileChangedEventArgs>();
        service.FileChanged += (_, e) => events.Add(e);

        try
        {
            service.Watch(root, ["*.csx"]);
            var original = Path.Combine(root, $"rename-me-{Guid.NewGuid():N}.csx");
            await File.WriteAllTextAsync(original, "// temp", TestContext.CancellationToken);
            await WaitForDebounceAsync();
            var renamed = Path.Combine(root, $"renamed-{Guid.NewGuid():N}.csx");
            File.Move(original, renamed);
            await WaitForDebounceAsync();

            Assert.IsTrue(events.Any(e => e.Scope == FileWatcherScope.FileContent && e.ChangeType == FileChangeType.Renamed));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [TestMethod]
    public async Task Watch_FileModify_RaisesModifiedEvent()
    {
        var root = CreateTempRoot();
        using var service = new FileWatcherService();
        var events = new List<FileChangedEventArgs>();
        service.FileChanged += (_, e) => events.Add(e);

        try
        {
            service.Watch(root, ["*.csx"]);
            var scriptPath = Path.Combine(root, $"modify-me-{Guid.NewGuid():N}.csx");
            await File.WriteAllTextAsync(scriptPath, "// v1", TestContext.CancellationToken);
            await WaitForDebounceAsync();
            await File.WriteAllTextAsync(scriptPath, "// v2", TestContext.CancellationToken);
            await WaitForDebounceAsync();

            Assert.IsTrue(events.Any(e => e.Scope == FileWatcherScope.FileContent && e.ChangeType == FileChangeType.Modified));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [TestMethod]
    public async Task UnwatchAll_StopsAllNotifications()
    {
        var root = CreateTempRoot();
        using var service = new FileWatcherService();
        var events = new List<FileChangedEventArgs>();
        service.FileChanged += (_, e) => events.Add(e);

        try
        {
            service.Watch(root, ["*.csx", "*.py"]);
            service.UnwatchAll();
            await File.WriteAllTextAsync(Path.Combine(root, "a.csx"), "x", TestContext.CancellationToken);
            await WaitForDebounceAsync();
            Assert.IsEmpty(events);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [TestMethod]
    public void Watch_NonExistentParentDirectory_IsNoOp()
    {
        using var service = new FileWatcherService();
        var missing = Path.Combine(Path.GetTempPath(), $"missing-parent-{Guid.NewGuid():N}", "child");
        service.Watch(missing, ["*.csx"]);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fw-ext-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private async Task WaitForDebounceAsync() =>
        await Task.Delay(DebounceWait, TestContext.CancellationToken);
}
