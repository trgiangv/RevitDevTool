using DevTools.Hosting;

namespace DevTools.Hosting.Tests;

public sealed class HostAppExtensionsTests
{
    [Theory]
    [InlineData(".rvt", HostApp.Revit)]
    [InlineData(".rfa", HostApp.Revit)]
    [InlineData(".rft", HostApp.Revit)]
    [InlineData(".rte", HostApp.Revit)]
    [InlineData(".dwg", HostApp.AutoCad)]
    [InlineData(".dxf", HostApp.AutoCad)]
    [InlineData(".dwf", HostApp.AutoCad)]
    [InlineData(".dwt", HostApp.AutoCad)]
    [InlineData(".nwd", HostApp.Navisworks)]
    [InlineData(".nwc", HostApp.Navisworks)]
    [InlineData(".nwf", HostApp.Navisworks)]
    public void FromExtension_maps_known_extensions(string extension, HostApp expected)
    {
        Assert.Equal(expected, HostAppExtensions.FromExtension(extension));
    }

    [Theory]
    [InlineData(".RVT", HostApp.Revit)]
    [InlineData(".DwG", HostApp.AutoCad)]
    [InlineData(".NWD", HostApp.Navisworks)]
    public void FromExtension_is_case_insensitive(string extension, HostApp expected)
    {
        Assert.Equal(expected, HostAppExtensions.FromExtension(extension));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(".unknown")]
    [InlineData("rvt")]
    public void FromExtension_returns_null_for_unknown_or_empty(string? extension)
    {
        Assert.Null(HostAppExtensions.FromExtension(extension));
    }

    [Fact]
    public void FromExtension_dwg_is_never_civil3d()
    {
        Assert.Equal(HostApp.AutoCad, HostAppExtensions.FromExtension(".dwg"));
        Assert.NotEqual(HostApp.Civil3D, HostAppExtensions.FromExtension(".dwg"));
    }

    [Theory]
    [InlineData(HostApp.AutoCad)]
    [InlineData(HostApp.Civil3D)]
    [InlineData(HostApp.Plant3D)]
    [InlineData(HostApp.AcadArch)]
    [InlineData(HostApp.AcadMech)]
    [InlineData(HostApp.AcadElec)]
    [InlineData(HostApp.AcadMep)]
    [InlineData(HostApp.AcadMap3D)]
    public void IsAcadFamily_is_true_for_autocad_family(HostApp host)
    {
        Assert.True(host.IsAcadFamily());
    }

    [Theory]
    [InlineData(HostApp.Revit)]
    [InlineData(HostApp.Navisworks)]
    [InlineData(HostApp.Rhino)]
    [InlineData(HostApp.Tekla)]
    public void IsAcadFamily_is_false_outside_autocad_family(HostApp host)
    {
        Assert.False(host.IsAcadFamily());
    }
}
