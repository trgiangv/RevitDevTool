using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Tests;

public sealed class PixiArgsTests
{
    [Fact]
    public void Add_CondaThenPypi_OnlyPypiGetsFlag()
    {
        Assert.Equal(["add", "numpy>=1.26", "gdal"], PixiEnvironmentProvider.PixiArgs.Add(["numpy>=1.26", "gdal"]));
        Assert.Equal(["add", "--pypi", "requests>=2.31"], PixiEnvironmentProvider.PixiArgs.Add(["requests>=2.31"], pypi: true));
    }

    [Fact]
    public void Remove_AndSearch_ShareFlagPlacement()
    {
        Assert.Equal(["remove", "numpy"], PixiEnvironmentProvider.PixiArgs.Remove("numpy"));
        Assert.Equal(["remove", "--pypi", "mcp"], PixiEnvironmentProvider.PixiArgs.Remove("mcp", pypi: true));
        Assert.Equal(["search", "--limit", "1", "numpy"], PixiEnvironmentProvider.PixiArgs.Search("numpy"));
    }

    [Fact]
    public void Install_List_Update_AreFixedArgv()
    {
        Assert.Equal(["install"], PixiEnvironmentProvider.PixiArgs.Install());
        Assert.Equal(["list", "--json"], PixiEnvironmentProvider.PixiArgs.ListJson());
        Assert.Equal(["list", "--explicit", "--json"], PixiEnvironmentProvider.PixiArgs.ListExplicitJson());
        Assert.Equal(["update", "packaging"], PixiEnvironmentProvider.PixiArgs.Update("packaging"));
    }
}
