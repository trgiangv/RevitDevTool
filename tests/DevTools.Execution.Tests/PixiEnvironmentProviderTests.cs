using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Tests;

/// <summary>
/// Unit coverage for <see cref="PixiEnvironmentProvider"/> helpers
/// (partition + package-name extract). No live pixi.exe.
/// </summary>
public sealed class PixiEnvironmentProviderTests
{
    [Fact]
    public void Partition_PrefersConda_WhenSearchHit()
    {
        var condaNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "numpy", "gdal" };

        var (conda, pypi) = PixiEnvironmentProvider.PartitionByAvailability(
            ["numpy>=1.26", "requests>=2.31", "gdal"],
            name => condaNames.Contains(name));

        Assert.Equal(["numpy>=1.26", "gdal"], conda);
        Assert.Equal(["requests>=2.31"], pypi);
    }

    [Fact]
    public void Partition_CondaOnlyName_GoesToConda_WithoutToolPixi()
    {
        var (conda, pypi) = PixiEnvironmentProvider.PartitionByAvailability(
            ["libgdal"],
            name => name.Equals("libgdal", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(["libgdal"], conda);
        Assert.Empty(pypi);
    }

    [Fact]
    public void Partition_DedupesByPackageName()
    {
        var (conda, pypi) = PixiEnvironmentProvider.PartitionByAvailability(
            ["numpy", "numpy>=1.26"],
            _ => true);

        Assert.Single(conda);
        Assert.Empty(pypi);
    }

    [Fact]
    public void ExtractPackageName_StripsPep723Constraints()
    {
        Assert.Equal("requests", PyEnvironmentProvider.ExtractPackageName("requests>=2.31.0"));
        Assert.Equal("mcp", PyEnvironmentProvider.ExtractPackageName("mcp>=2.0,<3"));
        Assert.Equal("packaging", PyEnvironmentProvider.ExtractPackageName("packaging"));
        Assert.Equal("httpx", PyEnvironmentProvider.ExtractPackageName("httpx[http2]>=0.27"));
        Assert.Equal(string.Empty, PyEnvironmentProvider.ExtractPackageName(""));
    }
}
