using System.Reflection;
using DevTools.Execution.Models;
using DevTools.Execution.Services;

namespace DevTools.Execution.Tests;

public sealed class PixiPackageStoreParseTests
{
    [Fact]
    public void ParseExplicitList_ReturnsEmpty_ForNonArrayJson()
    {
        Assert.Empty(Parse("""{"name":"numpy"}"""));
    }

    [Fact]
    public void ParseExplicitList_ParsesCondaAndPyPiEntries()
    {
        var packages = Parse("""
            [
              {"kind":"conda","name":"numpy","version":"2.0.0","requested_spec":"numpy=2.0.0"},
              {"kind":"pypi","name":"pytest","version":"9.1.1","requested_spec":"\"pytest==9.1.1\""}
            ]
            """);

        Assert.Equal(2, packages.Count);

        var numpy = packages[0];
        Assert.Equal(Marketplace.CondaForge, numpy.Marketplace);
        Assert.Equal("numpy", numpy.PackageId);
        Assert.Equal("2.0.0", numpy.Version);
        Assert.Equal("numpy=2.0.0", numpy.DeclaredVersion);

        var pytest = packages[1];
        Assert.Equal(Marketplace.PyPi, pytest.Marketplace);
        Assert.Equal("pytest", pytest.PackageId);
        Assert.Equal("pytest==9.1.1", pytest.DeclaredVersion);
        Assert.True(pytest.IsProtected);
    }

    [Fact]
    public void ParseExplicitList_SkipsPythonCondaEntryAndUnknownKinds()
    {
        var packages = Parse("""
            [
              {"kind":"conda","name":"python","version":"3.12.0"},
              {"kind":"npm","name":"left-pad","version":"1.0.0"},
              {"kind":"pypi","name":"  "}
            ]
            """);

        Assert.Empty(packages);
    }

    private static IReadOnlyList<Package> Parse(string json)
    {
        var method = typeof(PixiPackageStore).GetMethod(
            "ParseExplicitList",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        return (IReadOnlyList<Package>)method.Invoke(null, [json])!;
    }
}
