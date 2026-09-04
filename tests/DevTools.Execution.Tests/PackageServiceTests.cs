using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Hosting;
using DevTools.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

public sealed class PackageServiceTests
{
    [Fact]
    public async Task ListInstalledPackagesAsync_ReturnsNuGetPackagesWhenPythonUnavailable()
    {
        await using var provider = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var service = provider.GetRequiredService<IPackageService>();

        var packages = await service.ListInstalledPackagesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(packages);
    }

    [Fact]
    public async Task RemovePackageAsync_ProtectedPackage_IsNoOp()
    {
        await using var provider = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var service = provider.GetRequiredService<IPackageService>();
        var package = new Package(Marketplace.NuGet, "protected", "1.0", "1.0") { IsProtected = true };

        await service.RemovePackageAsync(package, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void PackageTreeNode_ChildNodes_ReflectsObservableCollection()
    {
        var marketplace = new MarketplaceNode(Marketplace.PyPi);
        var item = new PackageItemNode(new Package(Marketplace.PyPi, "requests", "2.32.0", "2.32.0"));
        marketplace.Children.Add(item);

        Assert.Single(marketplace.Children);
        Assert.Single(marketplace.ChildNodes);
        Assert.Same(item, marketplace.Children[0]);
    }

    [Fact]
    public async Task RemoveAllAsync_NuGet_ClearsCacheWhenEmpty()
    {
        await using var provider = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var service = provider.GetRequiredService<IPackageService>();

        await service.RemoveAllAsync(Marketplace.NuGet, TestContext.Current.CancellationToken);
    }

    [Fact]
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
            await service.RemovePackageAsync(new Package(Marketplace.NuGet, packageId, null, null), TestContext.Current.CancellationToken);
            Assert.False(Directory.Exists(packageDir));
        }
        finally
        {
            if (Directory.Exists(packageDir))
                Directory.Delete(packageDir, recursive: true);
        }
    }

    [Fact]
    public async Task RepairAsync_NuGetPackage_DoesNotThrow()
    {
        await using var provider = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var service = provider.GetRequiredService<IPackageService>();

        await service.RepairAsync(
            new Package(Marketplace.NuGet, "Newtonsoft.Json", "13.0.3", "13.0.3"),
            TestContext.Current.CancellationToken);
    }
}
