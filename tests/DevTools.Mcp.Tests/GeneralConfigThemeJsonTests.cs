using System.Text.Json;
using DevTools.Mcp.Catalog;
using DevTools.Settings;
using DevTools.Settings.Configs;

namespace DevTools.Mcp.Tests;

public class GeneralConfigThemeJsonTests
{
    private static string GoldenTheme0Path =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "general-config-theme-0.json");

    [Fact]
    public void Ordinals_MatchUiThemeContract()
    {
        Assert.Equal(0, (int)AppTheme.Light);
        Assert.Equal(1, (int)AppTheme.Dark);
        Assert.Equal(2, (int)AppTheme.Auto);
    }

    [Fact]
    public void GoldenNumericTheme0_DeserializesToLight()
    {
        var json = File.ReadAllText(GoldenTheme0Path);
        Assert.Contains("\"theme\": 0", json, StringComparison.Ordinal);

        var config = JsonSerializer.Deserialize<GeneralConfig>(json);
        Assert.NotNull(config);
        Assert.Equal(AppTheme.Light, config.Theme);
    }

    [Theory]
    [InlineData(0, AppTheme.Light)]
    [InlineData(1, AppTheme.Dark)]
    [InlineData(2, AppTheme.Auto)]
    public void NumericTheme_DeserializesToMatchingOrdinal(int stored, AppTheme expected)
    {
        var json = $"{{\"theme\":{stored}}}";
        var config = JsonSerializer.Deserialize<GeneralConfig>(json);
        Assert.NotNull(config);
        Assert.Equal(expected, config.Theme);
    }

    [Fact]
    public void SettingsAssembly_DoesNotReferenceUiOrMahApps()
    {
        AssertNoUiOrMahApps(typeof(GeneralConfig).Assembly);
        AssertNoUiOrMahApps(typeof(ISettingsService).Assembly);
    }

    [Fact]
    public void CatalogAssembly_DoesNotReferenceUiOrMahApps()
    {
        AssertNoUiOrMahApps(typeof(McpCatalogStore).Assembly);
    }

    private static void AssertNoUiOrMahApps(System.Reflection.Assembly assembly)
    {
        var names = assembly.GetReferencedAssemblies().Select(static name => name.Name).ToArray();
        Assert.DoesNotContain("DevTools.UI", names);
        Assert.DoesNotContain("MahApps.Metro", names);
        Assert.DoesNotContain("DevTools.MahApps.Metro", names);
        Assert.DoesNotContain("PresentationFramework", names);
    }
}
