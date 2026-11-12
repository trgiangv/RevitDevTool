using DevTools.AssemblyIsolation.Metadata;

namespace DevTools.AssemblyIsolation.Tests;

[TestClass]
public sealed class MetadataResolutionPathsTests
{
    [TestMethod]
    public void Paths_cover_entry_runtime_and_anchors()
    {
        using var directory = new TemporaryDirectory();
        var entry = MetadataAssemblySessionTests.CopyFixture("IsolationSibling", "IsolationSibling.dll", directory.Path);

        var paths = MetadataResolutionPaths.Collect(entry, typeof(MetadataScan));

        Assert.Contains(entry, paths);
        Assert.Contains(typeof(object).Assembly.Location, paths);
        Assert.Contains(typeof(MetadataScan).Assembly.Location, paths);
    }

    [TestMethod]
    public void Paths_include_loaded_assemblies()
    {
        var paths = MetadataResolutionPaths.Collect(typeof(MetadataResolutionPathsTests).Assembly.Location);

        Assert.Contains(typeof(TestClassAttribute).Assembly.Location, paths);
    }
}
