using DevTools.Execution.Providers.FSharp;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

[Collection(nameof(NugetRestoreCollection))]
public sealed class FSharpNugetManagerTests
{
    [Fact]
    public async Task FetchLatestVersionAsync_ReturnsVersionForKnownPackage()
    {
        var manager = new NugetManager(NullLogger<NugetManager>.Instance);

        var version = await manager.FetchLatestVersionAsync("Newtonsoft.Json", TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    [Fact]
    public async Task ResolvePackageDllsAsync_UsesSessionCacheOnSecondCall()
    {
        var manager = new NugetManager(NullLogger<NugetManager>.Instance);

        var first = await manager.ResolvePackageDllsAsync("Newtonsoft.Json", "13.0.3", TestContext.Current.CancellationToken);
        var second = await manager.ResolvePackageDllsAsync("Newtonsoft.Json", "13.0.3", TestContext.Current.CancellationToken);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task ResolvePackageDllsAsync_WithNullVersion_ResolvesLatest()
    {
        var manager = new NugetManager(NullLogger<NugetManager>.Instance);

        var dlls = await manager.ResolvePackageDllsAsync("Newtonsoft.Json", version: null, TestContext.Current.CancellationToken);

        Assert.NotEmpty(dlls);
        Assert.All(dlls, path => Assert.True(File.Exists(path)));
    }

    [Fact]
    public async Task ResolvePackageDllsAsync_ForFSharpCore_ReturnsCompatibleDlls()
    {
        var manager = new NugetManager(NullLogger<NugetManager>.Instance);

        var dlls = await manager.ResolvePackageDllsAsync("FSharp.Core", "9.0.100", TestContext.Current.CancellationToken);

        Assert.NotEmpty(dlls);
        Assert.Contains(dlls, path => Path.GetFileName(path).Equals("FSharp.Core.dll", StringComparison.OrdinalIgnoreCase));
    }
}
