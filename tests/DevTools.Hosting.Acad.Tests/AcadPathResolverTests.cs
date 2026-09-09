using DevTools.Hosting;
using DevTools.Hosting.Acad;

namespace DevTools.Hosting.Acad.Tests;

public sealed class AcadPathResolverTests
{
    [Fact]
    public void Parse_install_dir_named_AutoCAD_year()
    {
        using var dir = new TempAcadDir("AutoCAD 2026");
        Assert.True(AcadPathResolver.TryParseInstallDir(dir.Dir, out var exe, out var year));
        Assert.Equal("2026", year);
        Assert.Equal(dir.ExePath, exe);
    }

    [Fact]
    public void Parse_rejects_year_below_HostVersions_AutodeskMinimal()
    {
        using var dir = new TempAcadDir($"AutoCAD {HostVersions.AutodeskMinimal - 1}");
        Assert.False(AcadPathResolver.TryParseInstallDir(dir.Dir, out _, out _));
    }

    [Fact]
    public void Catalog_product_codes_match_InstalledProducts_and_product_switch()
    {
        Assert.Equal("C3D", AcadProductCatalog.ProductCodes[HostApp.Civil3D]);
        Assert.Equal("PLNT3D", AcadProductCatalog.ProductCodes[HostApp.Plant3D]);
        Assert.Equal("ACAD", AcadProductCatalog.ProductCodes[HostApp.AutoCad]);
        Assert.True(AcadProductCatalog.TryGetHost("C3D", out var civil));
        Assert.Equal(HostApp.Civil3D, civil);
        Assert.True(AcadProductCatalog.TryGetHost("PLNT3D", out var plant));
        Assert.Equal(HostApp.Plant3D, plant);
        Assert.Equal(AcadProductCatalog.ExeFileName, "acad.exe");
        Assert.Equal(AcadProductCatalog.CivilDbxFileName, "AecBase.dbx");
    }

    [Fact]
    public void FindExecutable_Plant3D_2027_resolves_this_machine_when_installed()
    {
        var path = new AcadPathResolver().FindExecutable(HostApp.Plant3D, "2027");
        if (path is null)
            Assert.Skip("Plant3D 2027 is not installed on this machine.");

        Assert.True(File.Exists(path));
        Assert.EndsWith(AcadProductCatalog.ExeFileName, path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2027", new AcadPathResolver().GetInstalledVersions(HostApp.Plant3D));
    }

    [Fact]
    public void FindExecutable_Civil3D_2026_resolves_this_machine_when_installed()
    {
        var path = new AcadPathResolver().FindExecutable(HostApp.Civil3D, "2026");
        if (path is null)
            Assert.Skip("Civil3D 2026 is not installed on this machine.");

        Assert.True(File.Exists(path));
        Assert.EndsWith(AcadProductCatalog.ExeFileName, path, StringComparison.OrdinalIgnoreCase);
        Assert.True(
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
