using DevTools.Execution.Providers.FSharp;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class FSharpNugetManagerTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FetchLatestVersionAsync_ReturnsVersionForKnownPackage()
    {
        var manager = new NugetManager(NullLogger<NugetManager>.Instance);

        var version = await manager.FetchLatestVersionAsync("Newtonsoft.Json", TestContext.CancellationToken);

        Assert.IsFalse(string.IsNullOrWhiteSpace(version));
    }

    [TestMethod]
    public async Task ResolvePackageDllsAsync_UsesSessionCacheOnSecondCall()
    {
        var manager = new NugetManager(NullLogger<NugetManager>.Instance);

        var first = await manager.ResolvePackageDllsAsync("Newtonsoft.Json", "13.0.3", TestContext.CancellationToken);
        var second = await manager.ResolvePackageDllsAsync("Newtonsoft.Json", "13.0.3", TestContext.CancellationToken);

        Assert.IsNotEmpty(first);
        CollectionAssert.AreEqual(first.ToList(), second.ToList());
    }

    [TestMethod]
    public async Task ResolvePackageDllsAsync_WithNullVersion_ResolvesLatest()
    {
        var manager = new NugetManager(NullLogger<NugetManager>.Instance);

        var dlls = await manager.ResolvePackageDllsAsync("Newtonsoft.Json", version: null, TestContext.CancellationToken);

        Assert.IsNotEmpty(dlls);
        foreach (var path in dlls)
            Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public async Task ResolvePackageDllsAsync_ForFSharpCore_ReturnsCompatibleDlls()
    {
        var manager = new NugetManager(NullLogger<NugetManager>.Instance);

        var dlls = await manager.ResolvePackageDllsAsync("FSharp.Core", "9.0.100", TestContext.CancellationToken);

        Assert.IsNotEmpty(dlls);
        Assert.Contains(
            path => Path.GetFileName(path).Equals("FSharp.Core.dll", StringComparison.OrdinalIgnoreCase),
            dlls);
    }
}
