using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Tests;

/// <summary>
/// Unit coverage for <see cref="PixiEnvironmentProvider"/> helpers
/// (partition + package-name extract). No live pixi.exe.
/// </summary>
[TestClass]
public sealed class PixiEnvironmentProviderTests
{
    [TestMethod]
    public void Partition_PrefersConda_WhenSearchHit()
    {
        var condaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "numpy", "gdal" };

        var (conda, pypi) = PixiEnvironmentProvider.PartitionByAvailability(
            ["numpy>=1.26", "requests>=2.31", "gdal"],
            name => condaNames.Contains(name));

        Assert.AreSequenceEqual(["numpy>=1.26", "gdal"], conda);
        Assert.AreSequenceEqual(["requests>=2.31"], pypi);
    }

    [TestMethod]
    public void Partition_CondaOnlyName_GoesToConda_WithoutToolPixi()
    {
        var (conda, pypi) = PixiEnvironmentProvider.PartitionByAvailability(
            ["libgdal"],
            name => name.Equals("libgdal", StringComparison.OrdinalIgnoreCase));

        Assert.AreSequenceEqual(["libgdal"], conda);
        Assert.IsEmpty(pypi);
    }

    [TestMethod]
    public void Partition_DedupesByPackageName()
    {
        var (conda, pypi) = PixiEnvironmentProvider.PartitionByAvailability(
            ["numpy", "numpy>=1.26"],
            _ => true);

        Assert.AreEqual(1, conda.Count);
        Assert.IsEmpty(pypi);
    }

    [TestMethod]
    public void ExtractPackageName_StripsPep723Constraints()
    {
        Assert.AreEqual("requests", PyEnvironmentProvider.ExtractPackageName("requests>=2.31.0"));
        Assert.AreEqual("mcp", PyEnvironmentProvider.ExtractPackageName("mcp>=2.0,<3"));
        Assert.AreEqual("packaging", PyEnvironmentProvider.ExtractPackageName("packaging"));
        Assert.AreEqual("httpx", PyEnvironmentProvider.ExtractPackageName("httpx[http2]>=0.27"));
        Assert.AreEqual(string.Empty, PyEnvironmentProvider.ExtractPackageName(""));
    }
}
