using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.Tests;

[TestClass]
public sealed class TestingAssemblyPreflightTests
{
    [TestMethod]
    public void RequireManagedAssembly_rejects_a_missing_file()
    {
        var exception = Assert.ThrowsExactly<TestingGenerationBuildException>(() =>
            TestingGenerationFiles.RequireManagedAssembly(@"C:\missing\assembly.dll"));

        Assert.Contains("not found", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void RequireManagedAssembly_returns_the_full_path_of_a_managed_assembly()
    {
        var path = typeof(TestingAssemblyPreflightTests).Assembly.Location;

        var resolved = TestingGenerationFiles.RequireManagedAssembly(path);

        Assert.AreEqual(Path.GetFullPath(path), resolved);
    }
}
