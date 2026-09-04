using DevTools.Execution.Models;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Services;
using DevTools.Hosting;
using DevTools.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevTools.Execution.Tests;

[Collection(nameof(NugetRestoreCollection))]
public sealed class NugetPackageStoreTests
{
    private static string CacheRoot => Path.Combine(AppUtils.GetApplicationDataPath(), "nuget");

    [Fact]
    public void List_ReturnsPackagesFromCacheFolders()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var packageId = $"DevTools.TestPkg.{suffix}";
        var packageDir = Path.Combine(CacheRoot, $"{packageId}.9.9.9");
        Directory.CreateDirectory(packageDir);
        File.WriteAllText(Path.Combine(packageDir, $"{packageId}.nuspec"), "<package />");

        try
        {
            var store = CreateStore();
            var packages = store.List();

            Assert.Contains(packages, package => package.PackageId == packageId && package.Version == "9.9.9");
        }
        finally
        {
            if (Directory.Exists(packageDir))
                Directory.Delete(packageDir, recursive: true);
        }
    }

    [Fact]
    public async Task RemoveAsync_DeletesPackageFolder()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var packageId = $"DevTools.DeleteMe.{suffix}";
        var packageDir = Path.Combine(CacheRoot, packageId);
        Directory.CreateDirectory(packageDir);
        File.WriteAllText(Path.Combine(packageDir, $"{packageId}.nuspec"), "<package />");
        var store = CreateStore();
        var package = new Package(Marketplace.NuGet, packageId, null, null);

        await store.RemoveAsync(package, TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(packageDir));
    }

    [Fact]
    public async Task UpdateAsync_ResolvesKnownPackage()
    {
        var store = CreateStore();
        var package = new Package(Marketplace.NuGet, "Newtonsoft.Json", null, null);

        await store.UpdateAsync(package, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RemoveAsync_DeletesVersionedPackageFolder()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var packageId = $"DevTools.Versioned.{suffix}";
        var packageDir = Path.Combine(CacheRoot, $"{packageId}.1.0.0");
        Directory.CreateDirectory(packageDir);
        File.WriteAllText(Path.Combine(packageDir, $"{packageId}.nuspec"), "<package />");
        var store = CreateStore();
        var package = new Package(Marketplace.NuGet, packageId, "1.0.0", "1.0.0");

        await store.RemoveAsync(package, TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(packageDir));
    }

    [Fact]
    public async Task RepairAsync_RestoresKnownPackage()
    {
        var store = CreateStore();
        var package = new Package(Marketplace.NuGet, "Newtonsoft.Json", "13.0.3", "13.0.3");

        await store.RepairAsync(package, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void List_WhenCacheMissing_ReturnsEmpty()
    {
        var store = CreateStore();
        var original = CacheRoot;
        if (!Directory.Exists(original))
        {
            var packages = store.List();
            Assert.Empty(packages);
        }
    }

    private static NugetPackageStore CreateStore()
    {
        var hostInfo = new Mock<IHostAppInfo>();
        hostInfo.SetupGet(h => h.Host).Returns(HostApp.Revit);
        return new NugetPackageStore(
            new NugetManager(NullLogger<NugetManager>.Instance),
            hostInfo.Object,
            NullLogger<NugetPackageStore>.Instance);
    }
}
