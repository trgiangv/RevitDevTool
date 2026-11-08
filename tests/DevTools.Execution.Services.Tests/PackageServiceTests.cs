using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PackageServiceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ListInstalledPackagesAsync_ReturnsNuGetPackagesWhenPythonUnavailable()
    {
        await using var provider = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var service = provider.GetRequiredService<IPackageService>();

        var packages = await service.ListInstalledPackagesAsync(TestContext.CancellationToken);

        Assert.IsNotNull(packages);
    }

    [TestMethod]
    public async Task RemovePackageAsync_ProtectedPackage_IsNoOp()
    {
        var packageId = $"DevTools.Protected.{Guid.NewGuid():N}";
        var packageDir = Path.Combine(AppUtils.GetApplicationDataPath(), "nuget", packageId);
        Directory.CreateDirectory(packageDir);

        await using var provider = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var service = provider.GetRequiredService<IPackageService>();
        var package = new Package(Marketplace.NuGet, packageId, null, null) { IsProtected = true };

        try
        {
            await service.RemovePackageAsync(package, TestContext.CancellationToken);
            Assert.IsTrue(Directory.Exists(packageDir));
        }
        finally
        {
            Directory.Delete(packageDir, recursive: true);
        }
    }

    [TestMethod]
    public void PackageTreeNode_ChildNodes_ReflectsObservableCollection()
    {
        var marketplace = new MarketplaceNode(Marketplace.PyPi);
        var item = new PackageItemNode(new Package(Marketplace.PyPi, "requests", "2.32.0", "2.32.0"));
        marketplace.Children.Add(item);

        Assert.HasCount(1, marketplace.Children);
        Assert.HasCount(1, marketplace.ChildNodes);
        Assert.AreSame(item, marketplace.Children[0]);
    }

    [TestMethod]
    public async Task RemoveAllAsync_NuGet_DeletesCachedPackages()
    {
        var packageDir = Path.Combine(AppUtils.GetApplicationDataPath(), "nuget", $"DevTools.RemoveAll.{Guid.NewGuid():N}");
        Directory.CreateDirectory(packageDir);

        await using var provider = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var service = provider.GetRequiredService<IPackageService>();

        await service.RemoveAllAsync(Marketplace.NuGet, TestContext.CancellationToken);

        Assert.IsFalse(Directory.Exists(packageDir));
    }

    [TestMethod]
    public async Task RemovePackageAsync_UnprotectedNuGet_RemovesFromCache()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var packageId = $"DevTools.ServiceRemove.{suffix}";
        var packageDir = Path.Combine(
            Path.Combine(AppUtils.GetApplicationDataPath(), "nuget"),
            packageId);
        Directory.CreateDirectory(packageDir);
        File.WriteAllText(Path.Combine(packageDir, $"{packageId}.nuspec"), "<package />");

        await using var provider = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var service = provider.GetRequiredService<IPackageService>();

        try
        {
            await service.RemovePackageAsync(new Package(Marketplace.NuGet, packageId, null, null), TestContext.CancellationToken);
            Assert.IsFalse(Directory.Exists(packageDir));
        }
        finally
        {
            if (Directory.Exists(packageDir))
                Directory.Delete(packageDir, recursive: true);
        }
    }
}
