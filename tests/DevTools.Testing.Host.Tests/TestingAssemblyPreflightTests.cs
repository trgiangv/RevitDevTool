using DevTools.Testing.Host;

namespace DevTools.Testing.Host.Tests;

[TestClass]
public sealed class TestingAssemblyPreflightTests
{
    [TestMethod]
    public void Preflight_reports_a_missing_assembly_without_framework_types()
    {
        var result = TestingAssemblyPreflight.Check(@"C:\missing\assembly.dll");

        Assert.IsFalse(result.Success);
        Assert.Contains("not found", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void ResolveAndEnsureLoadable_returns_the_normalized_managed_assembly_path()
    {
        var path = typeof(TestingAssemblyPreflightTests).Assembly.Location;

        var resolved = TestingAssemblyPreflight.ResolveAndEnsureLoadable(path);

        Assert.AreEqual(Path.GetFullPath(path), resolved);
    }
}
