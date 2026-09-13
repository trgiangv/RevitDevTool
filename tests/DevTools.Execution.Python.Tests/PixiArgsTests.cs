using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PixiArgsTests
{
    [TestMethod]
    public void Add_CondaThenPypi_OnlyPypiGetsFlag()
    {
        Assert.AreSequenceEqual(["add", "numpy>=1.26", "gdal"], PixiEnvironmentProvider.PixiArgs.Add(["numpy>=1.26", "gdal"]));
        Assert.AreSequenceEqual(["add", "--pypi", "requests>=2.31"], PixiEnvironmentProvider.PixiArgs.Add(["requests>=2.31"], pypi: true));
    }

    [TestMethod]
    public void Remove_AndSearch_ShareFlagPlacement()
    {
        Assert.AreSequenceEqual(["remove", "numpy"], PixiEnvironmentProvider.PixiArgs.Remove("numpy"));
        Assert.AreSequenceEqual(["remove", "--pypi", "mcp"], PixiEnvironmentProvider.PixiArgs.Remove("mcp", pypi: true));
        Assert.AreSequenceEqual(["search", "--limit", "1", "numpy"], PixiEnvironmentProvider.PixiArgs.Search("numpy"));
    }

    [TestMethod]
    public void Install_List_Update_AreFixedArgv()
    {
        Assert.AreSequenceEqual(["install"], PixiEnvironmentProvider.PixiArgs.Install());
        Assert.AreSequenceEqual(["list", "--json"], PixiEnvironmentProvider.PixiArgs.ListJson());
        Assert.AreSequenceEqual(["list", "--explicit", "--json"], PixiEnvironmentProvider.PixiArgs.ListExplicitJson());
        Assert.AreSequenceEqual(["update", "packaging"], PixiEnvironmentProvider.PixiArgs.Update("packaging"));
    }
}
