using DevTools.Hosting;
using DevTools.Hosting.Acad;

namespace DevTools.Hosting.Acad.Tests;

[TestClass]
public sealed class AcadArgumentBuilderTests
{
    private readonly AcadArgumentBuilder _builder = new();

    [TestMethod]
    public void Civil3D_includes_ld_metric_profile_product_and_en_US()
    {
        using var dir = new TempInstallDir(withDbx: true);
        var args = _builder.Build(
            new HostLaunchRequest(HostApp.Civil3D, "2026", null, null),
            dir.ExePath);

        CollectionAssert.AreEqual(
            new[]
            {
                "/ld", dir.DbxPath,
                "/p", AcadArgumentBuilder.CivilMetricProfile,
                "/product", "C3D",
                "/language", "en-US"
            },
            args.ToArray());
        Assert.DoesNotContain("/nologo", args, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("/nosplash", args, StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Civil3D_fails_closed_when_AecBase_dbx_is_missing()
    {
        using var dir = new TempInstallDir(withDbx: false);
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => _builder.Build(
            new HostLaunchRequest(HostApp.Civil3D, "2026", null, null),
            dir.ExePath));
        Assert.Contains("AecBase.dbx", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Plant3D_is_product_and_language_without_ld()
    {
        using var dir = new TempInstallDir(withDbx: false);
        var args = _builder.Build(
            new HostLaunchRequest(HostApp.Plant3D, "2027", null, null),
            dir.ExePath);

        CollectionAssert.AreEqual(new[] { "/product", "PLNT3D", "/language", "en-US" }, args.ToArray());
        Assert.DoesNotContain("/ld", args, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("/p", args, StringComparer.Ordinal);
        Assert.DoesNotContain("/nologo", args, StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    [DataRow(HostApp.AutoCad, "ACAD")]
    [DataRow(HostApp.AcadMap3D, "MAP")]
    [DataRow(HostApp.AcadArch, "ACA")]
    [DataRow(HostApp.AcadMech, "ACADM")]
    [DataRow(HostApp.AcadMep, "MEP")]
    [DataRow(HostApp.AcadElec, "ACADE")]
    public void Other_family_hosts_are_product_and_en_US_only(HostApp host, string product)
    {
        using var dir = new TempInstallDir(withDbx: false);
        var args = _builder.Build(new HostLaunchRequest(host, "2026", null, null), dir.ExePath);
        CollectionAssert.AreEqual(new[] { "/product", product, "/language", "en-US" }, args.ToArray());
        Assert.DoesNotContain("/p", args, StringComparer.Ordinal);
        Assert.DoesNotContain("/ld", args, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("/nologo", args, StringComparer.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Acad_language_option_is_passed_through_as_culture()
    {
        using var dir = new TempInstallDir(withDbx: false);
        var args = _builder.Build(
            new HostLaunchRequest(
                HostApp.AutoCad,
                "2026",
                null,
                new Dictionary<string, string> { [HostLaunchRequest.LanguageOptionKey] = "en-US" }),
            dir.ExePath);
        Assert.Contains("en-US", args);
        Assert.DoesNotContain("ENU", args);
    }

    private sealed class TempInstallDir : IDisposable
    {
        private readonly string _dir;

        public TempInstallDir(bool withDbx)
        {
            _dir = Path.Combine(Path.GetTempPath(), "acad-launch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            ExePath = Path.Combine(_dir, "acad.exe");
            File.WriteAllText(ExePath, "");
            DbxPath = Path.Combine(_dir, "AecBase.dbx");
            if (withDbx)
                File.WriteAllText(DbxPath, "");
        }

        public string ExePath { get; }
        public string DbxPath { get; }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); }
            catch { /* temp */ }
        }
    }
}
