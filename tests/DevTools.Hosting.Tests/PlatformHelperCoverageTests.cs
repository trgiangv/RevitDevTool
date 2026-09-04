using DevTools.Hosting;
using DevTools.Hosting.Acad;
using DevTools.Hosting.Revit;
using DevTools.Utilities;

namespace DevTools.Hosting.Tests;

public sealed class HostLaunchRequestTests
{
    [Fact]
    public void LanguageCulture_defaults_to_en_US()
    {
        var request = new HostLaunchRequest(HostApp.Revit, "2025", null, null);
        Assert.Equal(HostLaunchRequest.DefaultLanguageCulture, request.LanguageCulture);
    }

    [Fact]
    public void LanguageCulture_reads_trimmed_option_value()
    {
        var request = new HostLaunchRequest(
            HostApp.AutoCad,
            "2026",
            null,
            new Dictionary<string, string> { [HostLaunchRequest.LanguageOptionKey] = "  fr-FR  " });
        Assert.Equal("fr-FR", request.LanguageCulture);
    }
}

public sealed class RevitVersionSelectorExtractYearTests
{
    [Theory]
    [InlineData("Autodesk Revit 2025", "2025")]
    [InlineData("build-2024.1", "2024")]
    [InlineData(null, null)]
    [InlineData("no-year", null)]
    public void ExtractYear_finds_first_20xx_token(string? input, string? expected)
    {
        Assert.Equal(expected, RevitVersionSelector.ExtractYear(input));
    }

    [Fact]
    public void FindCompatibleVersion_returns_null_when_nothing_installed()
    {
        Assert.Null(RevitVersionSelector.FindCompatibleVersion("2025", []));
    }
}

public sealed class AcadPathResolverProductIdTests
{
    [Theory]
    [InlineData("00", HostApp.Civil3D)]
    [InlineData("01", HostApp.AutoCad)]
    [InlineData("17", HostApp.Plant3D)]
    public void ProductIdMap_maps_registry_product_ids(string productId, HostApp expected)
    {
        Assert.True(AcadPathResolver.ProductIdMap.TryGetValue(productId, out var host));
        Assert.Equal(expected, host);
    }
}

public sealed class AppUtilsSmokeTests
{
    [Fact]
    public void Bundle_paths_use_RevitDevTool_bundle_layout()
    {
        Assert.EndsWith("DevTools.Daemon.exe", AppUtils.GetDaemonExePath(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RevitDevTool.bundle", AppUtils.GetBundleContentsPath(), StringComparison.OrdinalIgnoreCase);
    }
}
