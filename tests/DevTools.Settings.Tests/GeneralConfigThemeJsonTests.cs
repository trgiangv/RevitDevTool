using System.Text.Json;
using DevTools.Settings;
using DevTools.Settings.Configs;

namespace DevTools.Settings.Tests;

[TestClass]
public sealed class GeneralConfigThemeJsonTests
{
    private static string GoldenTheme0Path =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "general-config-theme-0.json");

    [TestMethod]
    public void Ordinals_MatchUiThemeContract()
    {
#pragma warning disable MSTEST0032 // enum ordinals document the UI theme wire contract.
        Assert.AreEqual(0, (int)AppTheme.Light);
        Assert.AreEqual(1, (int)AppTheme.Dark);
        Assert.AreEqual(2, (int)AppTheme.Auto);
#pragma warning restore MSTEST0032
    }

    [TestMethod]
    public void GoldenNumericTheme0_DeserializesToLight()
    {
        var json = File.ReadAllText(GoldenTheme0Path);
        Assert.Contains("\"theme\": 0", json, StringComparison.Ordinal);

        var config = JsonSerializer.Deserialize<GeneralConfig>(json);
        Assert.IsNotNull(config);
        Assert.AreEqual(AppTheme.Light, config.Theme);
    }

    [TestMethod]
    [DataRow(0, AppTheme.Light)]
    [DataRow(1, AppTheme.Dark)]
    [DataRow(2, AppTheme.Auto)]
    public void NumericTheme_DeserializesToMatchingOrdinal(int stored, AppTheme expected)
    {
        var json = $"{{\"theme\":{stored}}}";
        var config = JsonSerializer.Deserialize<GeneralConfig>(json);
        Assert.IsNotNull(config);
        Assert.AreEqual(expected, config.Theme);
    }

    [TestMethod]
    public void SettingsAssembly_DoesNotReferenceUiOrMahApps()
    {
        var names = typeof(GeneralConfig).Assembly.GetReferencedAssemblies()
            .Select(static name => name.Name)
            .ToArray();
        Assert.DoesNotContain("DevTools.UI", names);
        Assert.DoesNotContain("MahApps.Metro", names);
        Assert.DoesNotContain("DevTools.MahApps.Metro", names);
        Assert.DoesNotContain("PresentationFramework", names);

        names = typeof(ISettingsService).Assembly.GetReferencedAssemblies()
            .Select(static name => name.Name)
            .ToArray();
        Assert.DoesNotContain("DevTools.UI", names);
        Assert.DoesNotContain("MahApps.Metro", names);
        Assert.DoesNotContain("DevTools.MahApps.Metro", names);
        Assert.DoesNotContain("PresentationFramework", names);
    }
}
