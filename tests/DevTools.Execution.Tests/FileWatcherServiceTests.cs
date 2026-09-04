using DevTools.Execution.Abstractions;
using DevTools.Execution.Services;

namespace DevTools.Execution.Tests;

public sealed class FileWatcherServiceTests
{
    private static readonly TimeSpan DebounceWait = TimeSpan.FromMilliseconds(1000);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Watch_EmptyOrWhitespacePath_IsNoOp(string path)
    {
        var root = CreateTempRoot();
        using var service = new FileWatcherService();

        try
        {
            var ex = Record.Exception(() => service.Watch(path, ["*.csx"]));
            Assert.Null(ex);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task Watch_CsxFileChange_RaisesFileContentEventAfterDebounce()
    {
        var root = CreateTempRoot();
        using var service = new FileWatcherService();
        var events = new List<FileChangedEventArgs>();
        service.FileChanged += (_, e) => events.Add(e);

        try
        {
            service.Watch(root, ["*.csx"]);

            var scriptPath = Path.Combine(root, $"script-{Guid.NewGuid():N}.csx");
            await File.WriteAllTextAsync(scriptPath, "// initial", TestContext.Current.CancellationToken);
            await WaitForDebounceAsync();

            var fileContentEvent = events.LastOrDefault(e =>
                e.Scope == FileWatcherScope.FileContent
                && e.Path.EndsWith(".csx", StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(fileContentEvent);
            Assert.Equal(FileWatcherScope.FileContent, fileContentEvent.Scope);

            await File.WriteAllTextAsync(scriptPath, "// modified", TestContext.Current.CancellationToken);
            await WaitForDebounceAsync();

            var modifiedEvents = events.Where(e =>
                e.Scope == FileWatcherScope.FileContent
                && string.Equals(e.Path, scriptPath, StringComparison.OrdinalIgnoreCase)).ToList();

            Assert.True(modifiedEvents.Count >= 1);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public async Task Unwatch_AfterUnwatch_ModificationsDoNotRaiseEvents()
    {
        var root = CreateTempRoot();
        using var service = new FileWatcherService();
        var events = new List<FileChangedEventArgs>();
        service.FileChanged += (_, e) => events.Add(e);

        try
        {
            service.Watch(root, ["*.csx"]);

            var scriptPath = Path.Combine(root, $"script-{Guid.NewGuid():N}.csx");
            await File.WriteAllTextAsync(scriptPath, "// initial", TestContext.Current.CancellationToken);
            await WaitForDebounceAsync();

            Assert.Contains(events, e =>
                e.Scope == FileWatcherScope.FileContent
                && string.Equals(e.Path, scriptPath, StringComparison.OrdinalIgnoreCase));

            service.Unwatch(root);
            events.Clear();

            await File.WriteAllTextAsync(scriptPath, "// after unwatch", TestContext.Current.CancellationToken);
            await WaitForDebounceAsync();

            Assert.DoesNotContain(events, e =>
                e.Scope == FileWatcherScope.FileContent
                && string.Equals(e.Path, scriptPath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void Dispose_ThenWatch_ThrowsObjectDisposedException()
    {
        var root = CreateTempRoot();
        var service = new FileWatcherService();

        try
        {
            service.Dispose();

            Assert.Throws<ObjectDisposedException>(() => service.Watch(root, ["*.csx"]));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fw-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTempRoot(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private static async Task WaitForDebounceAsync()
    {
        await Task.Delay(DebounceWait, TestContext.Current.CancellationToken);
    }
}
