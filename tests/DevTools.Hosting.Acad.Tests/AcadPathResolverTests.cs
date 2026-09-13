using DevTools.Hosting;
using DevTools.Hosting.Acad;

namespace DevTools.Hosting.Acad.Tests;

[TestClass]
public sealed class AcadPathResolverTests
{
    [TestMethod]
    public void Parse_install_dir_named_AutoCAD_year()
    {
        using var dir = new TempAcadDir("AutoCAD 2026");
        Assert.IsTrue(AcadPathResolver.TryParseInstallDir(dir.Dir, out var exe, out var year));
        Assert.AreEqual("2026", year);
        Assert.AreEqual(dir.ExePath, exe);
    }

    [TestMethod]
    public void Parse_rejects_year_below_HostVersions_AutodeskMinimal()
    {
        using var dir = new TempAcadDir($"AutoCAD {HostVersions.AutodeskMinimal - 1}");
        Assert.IsFalse(AcadPathResolver.TryParseInstallDir(dir.Dir, out _, out _));
    }

    [TestMethod]
    public void Catalog_product_codes_match_InstalledProducts_and_product_switch()
    {
        Assert.AreEqual("C3D", AcadProductCatalog.ProductCodes[HostApp.Civil3D]);
        Assert.AreEqual("PLNT3D", AcadProductCatalog.ProductCodes[HostApp.Plant3D]);
        Assert.AreEqual("ACAD", AcadProductCatalog.ProductCodes[HostApp.AutoCad]);
        Assert.IsTrue(AcadProductCatalog.TryGetHost("C3D", out var civil));
        Assert.AreEqual(HostApp.Civil3D, civil);
        Assert.IsTrue(AcadProductCatalog.TryGetHost("PLNT3D", out var plant));
        Assert.AreEqual(HostApp.Plant3D, plant);
#pragma warning disable MSTEST0032 // Public const; test documents the catalog contract.
        Assert.AreEqual(AcadProductCatalog.ExeFileName, "acad.exe");
        Assert.AreEqual(AcadProductCatalog.CivilDbxFileName, "AecBase.dbx");
#pragma warning restore MSTEST0032
    }

    [TestMethod]
    public void FindExecutable_Plant3D_2027_resolves_this_machine_when_installed()
    {
        var path = new AcadPathResolver().FindExecutable(HostApp.Plant3D, "2027");
        if (path is null)
            Assert.Inconclusive("Plant3D 2027 is not installed on this machine.");

        Assert.IsTrue(File.Exists(path));
        Assert.EndsWith(AcadProductCatalog.ExeFileName, path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2027", new AcadPathResolver().GetInstalledVersions(HostApp.Plant3D));
    }

    [TestMethod]
    public void FindExecutable_Civil3D_2026_resolves_this_machine_when_installed()
    {
        var path = new AcadPathResolver().FindExecutable(HostApp.Civil3D, "2026");
        if (path is null)
            Assert.Inconclusive("Civil3D 2026 is not installed on this machine.");

        Assert.IsTrue(File.Exists(path));
        Assert.EndsWith(AcadProductCatalog.ExeFileName, path, StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(
            File.Exists(Path.Combine(Path.GetDirectoryName(path)!, AcadProductCatalog.CivilDbxFileName)),
            "Civil3D /ld expects AecBase.dbx next to acad.exe.");
        Assert.Contains("2026", new AcadPathResolver().GetInstalledVersions(HostApp.Civil3D));
    }

    private sealed class TempAcadDir : IDisposable
    {
        public TempAcadDir(string folderName = "AutoCAD 2026")
        {
            Dir = Path.Combine(Path.GetTempPath(), "acad-path-" + Guid.NewGuid().ToString("N"), folderName);
            Directory.CreateDirectory(Dir);
            ExePath = Path.Combine(Dir, AcadProductCatalog.ExeFileName);
            File.WriteAllText(ExePath, "");
        }

        public string Dir { get; }
        public string ExePath { get; }

        public void Dispose()
        {
            try
            {
                var root = Path.GetDirectoryName(Dir);
                if (root is not null)
                    Directory.Delete(root, recursive: true);
            }
            catch { /* temp */ }
        }
    }
}
