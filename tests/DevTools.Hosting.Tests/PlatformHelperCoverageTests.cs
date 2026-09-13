using DevTools.Hosting;
using DevTools.Hosting.Acad;
using DevTools.Hosting.Revit;
using DevTools.Utilities;

namespace DevTools.Hosting.Tests;

[TestClass]
public sealed class HostLaunchRequestTests
{
    [TestMethod]
    public void LanguageCulture_defaults_to_en_US()
    {
        var request = new HostLaunchRequest(HostApp.Revit, "2025", null, null);
        Assert.AreEqual(HostLaunchRequest.DefaultLanguageCulture, request.LanguageCulture);
    }

    [TestMethod]
    public void LanguageCulture_reads_trimmed_option_value()
    {
        var request = new HostLaunchRequest(
            HostApp.AutoCad,
            "2026",
            null,
            new Dictionary<string, string> { [HostLaunchRequest.LanguageOptionKey] = "  fr-FR  " });
        Assert.AreEqual("fr-FR", request.LanguageCulture);
    }
}

[TestClass]
public sealed class RevitVersionSelectorExtractYearTests
{
    [TestMethod]
    [DataRow("Autodesk Revit 2025", "2025")]
    [DataRow("build-2024.1", "2024")]
    [DataRow(null, null)]
    [DataRow("no-year", null)]
    public void ExtractYear_finds_first_20xx_token(string? input, string? expected)
    {
        Assert.AreEqual(expected, RevitVersionSelector.ExtractYear(input));
    }

    [TestMethod]
    public void FindCompatibleVersion_returns_null_when_nothing_installed()
    {
        Assert.IsNull(RevitVersionSelector.FindCompatibleVersion("2025", []));
    }
}

[TestClass]
public sealed class AcadPathResolverProductIdTests
{
    [TestMethod]
    [DataRow("00", HostApp.Civil3D)]
    [DataRow("01", HostApp.AutoCad)]
    [DataRow("17", HostApp.Plant3D)]
    public void ProductIdMap_maps_registry_product_ids(string productId, HostApp expected)
    {
        Assert.IsTrue(AcadPathResolver.ProductIdMap.TryGetValue(productId, out var host));
        Assert.AreEqual(expected, host);
    }
}

[TestClass]
public sealed class AppUtilsSmokeTests
{
    [TestMethod]
    public void Bundle_paths_use_RevitDevTool_bundle_layout()
    {
        Assert.EndsWith("DevTools.Daemon.exe", AppUtils.GetDaemonExePath(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RevitDevTool.bundle", AppUtils.GetBundleContentsPath(), StringComparison.OrdinalIgnoreCase);
    }
}
