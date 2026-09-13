using System.Reflection;
using DevTools.Execution.Models;
using DevTools.Execution.Services;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PixiPackageStoreParseTests
{
    [TestMethod]
    public void ParseExplicitList_ReturnsEmpty_ForNonArrayJson()
    {
        Assert.IsEmpty(Parse("""{"name":"numpy"}"""));
    }

    [TestMethod]
    public void ParseExplicitList_ParsesCondaAndPyPiEntries()
    {
        var packages = Parse("""
            [
              {"kind":"conda","name":"numpy","version":"2.0.0","requested_spec":"numpy=2.0.0"},
              {"kind":"pypi","name":"pytest","version":"9.1.1","requested_spec":"\"pytest==9.1.1\""}
            ]
            """);

        Assert.AreEqual(2, packages.Count);

        var numpy = packages[0];
        Assert.AreEqual(Marketplace.CondaForge, numpy.Marketplace);
        Assert.AreEqual("numpy", numpy.PackageId);
        Assert.AreEqual("2.0.0", numpy.Version);
        Assert.AreEqual("numpy=2.0.0", numpy.DeclaredVersion);

        var pytest = packages[1];
        Assert.AreEqual(Marketplace.PyPi, pytest.Marketplace);
        Assert.AreEqual("pytest", pytest.PackageId);
        Assert.AreEqual("pytest==9.1.1", pytest.DeclaredVersion);
        Assert.IsTrue(pytest.IsProtected);
    }

    [TestMethod]
    public void ParseExplicitList_SkipsPythonCondaEntryAndUnknownKinds()
    {
        var packages = Parse("""
            [
              {"kind":"conda","name":"python","version":"3.12.0"},
              {"kind":"npm","name":"left-pad","version":"1.0.0"},
              {"kind":"pypi","name":"  "}
            ]
            """);

        Assert.IsEmpty(packages);
    }

    private static IReadOnlyList<Package> Parse(string json)
    {
        var method = typeof(PixiPackageStore).GetMethod(
            "ParseExplicitList",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        return (IReadOnlyList<Package>)method.Invoke(null, [json])!;
    }
}
