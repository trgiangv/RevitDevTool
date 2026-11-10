using Build.Pipeline;
using Build.Products;

namespace Build.Tests;

[TestClass]
public sealed class AddinHostsTests
{
    private string _root = null!;

    [TestInitialize]
    public void Create() => _root = Directory.CreateTempSubdirectory("addin-hosts").FullName;

    [TestCleanup]
    public void Delete() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public void Empty_hosts_are_refused()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => AddinHosts.CompiledVersion(_root));
    }

    [TestMethod]
    public void Mixed_compiles_are_refused()
    {
        Stage("2025", "1.0.0-ci.1");
        Stage("2026", "1.0.0-ci.2");

        Assert.ThrowsExactly<InvalidOperationException>(() => AddinHosts.CompiledVersion(_root));
    }

    [TestMethod]
    public void Missing_host_version_is_named()
    {
        Stage("2026", "1.0.0-ci.1");

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => AddinHosts.Require(_root, ["2025", "2026"], "1.0.0-ci.1"));

        StringAssert.Contains(exception.Message, "2025");
        Assert.IsFalse(exception.Message.Contains("2026"));
    }

    [TestMethod]
    public void Complete_hosts_resolve()
    {
        Stage("2025", "1.0.0-ci.1");
        Stage("2026", "1.0.0-ci.1");

        Assert.AreEqual("1.0.0-ci.1", AddinHosts.CompiledVersion(_root));
        Assert.HasCount(2, AddinHosts.Require(_root, ["2025", "2026"], "1.0.0-ci.1"));
    }

    [TestMethod]
    public void Collect_splits_shared_and_host_files()
    {
        Publish("source/RevitDevTool", "RevitDevTool", ("RevitDevTool.dll", "revit"), ("Shared.dll", "same"));
        Publish("source/AcadDevTool", "AcadDevTool", ("AcadDevTool.dll", "acad"), ("Shared.dll", "same"));

        AddinHosts.Collect(Run(), "Release.Autodesk.2025");

        var folder = Path.Combine(AddinHosts.Folder(_root), "2025");
        CollectionAssert.AreEquivalent(
            new[] { @"AcadDevTool.bundle\AcadDevTool.dll", @"RevitDevTool.bundle\RevitDevTool.dll", @"shared\Shared.dll" },
            Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(folder, file)).ToArray());
    }

    [TestMethod]
    public void Different_shared_file_is_refused()
    {
        Publish("source/RevitDevTool", "RevitDevTool", ("Shared.dll", "one"));
        Publish("source/AcadDevTool", "AcadDevTool", ("Shared.dll", "two"));

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => AddinHosts.Collect(Run(), "Release.Autodesk.2025"));

        StringAssert.Contains(exception.Message, "Shared.dll");
    }

    private BuildRun Run() => new(_root, Product.Addin, Build.Pipeline.Stage.Compile, new ProductVersion(Product.Addin, "0.0.0-ci.1", true, null));

    private void Publish(string projectFolder, string bundle, params (string Name, string Text)[] files)
    {
        var publish = Path.Combine(_root, projectFolder, "bin", "Release.Autodesk.2025", "publish");
        var contents = Directory.CreateDirectory(Path.Combine(publish, "Contents", "2025")).FullName;
        File.WriteAllText(Path.Combine(publish, "PackageContents.xml"), $"<ApplicationPackage Name=\"{bundle}\" />");
        foreach (var (name, text) in files) File.WriteAllText(Path.Combine(contents, name), text);
    }

    private void Stage(string hostVersion, string version)
    {
        var folder = Directory.CreateDirectory(Path.Combine(AddinHosts.Folder(_root), hostVersion)).FullName;
        File.WriteAllText(folder + ".version", version);
    }
}
