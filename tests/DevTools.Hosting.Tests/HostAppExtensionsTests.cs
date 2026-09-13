using DevTools.Hosting;

namespace DevTools.Hosting.Tests;

[TestClass]
public sealed class HostAppExtensionsTests
{
    [TestMethod]
    [DataRow(".rvt", HostApp.Revit)]
    [DataRow(".rfa", HostApp.Revit)]
    [DataRow(".rft", HostApp.Revit)]
    [DataRow(".rte", HostApp.Revit)]
    [DataRow(".dwg", HostApp.AutoCad)]
    [DataRow(".dxf", HostApp.AutoCad)]
    [DataRow(".dwf", HostApp.AutoCad)]
    [DataRow(".dwt", HostApp.AutoCad)]
    [DataRow(".nwd", HostApp.Navisworks)]
    [DataRow(".nwc", HostApp.Navisworks)]
    [DataRow(".nwf", HostApp.Navisworks)]
    public void FromExtension_maps_known_extensions(string extension, HostApp expected)
    {
        Assert.AreEqual(expected, HostAppExtensions.FromExtension(extension));
    }

    [TestMethod]
    [DataRow(".RVT", HostApp.Revit)]
    [DataRow(".DwG", HostApp.AutoCad)]
    [DataRow(".NWD", HostApp.Navisworks)]
    public void FromExtension_is_case_insensitive(string extension, HostApp expected)
    {
        Assert.AreEqual(expected, HostAppExtensions.FromExtension(extension));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(".unknown")]
    [DataRow("rvt")]
    public void FromExtension_returns_null_for_unknown_or_empty(string? extension)
    {
        Assert.IsNull(HostAppExtensions.FromExtension(extension));
    }

    [TestMethod]
    public void FromExtension_dwg_is_never_civil3d()
    {
        Assert.AreEqual(HostApp.AutoCad, HostAppExtensions.FromExtension(".dwg"));
        Assert.AreNotEqual(HostApp.Civil3D, HostAppExtensions.FromExtension(".dwg"));
    }

    [TestMethod]
    [DataRow(HostApp.AutoCad)]
    [DataRow(HostApp.Civil3D)]
    [DataRow(HostApp.Plant3D)]
    [DataRow(HostApp.AcadArch)]
    [DataRow(HostApp.AcadMech)]
    [DataRow(HostApp.AcadElec)]
    [DataRow(HostApp.AcadMep)]
    [DataRow(HostApp.AcadMap3D)]
    public void IsAcadFamily_is_true_for_autocad_family(HostApp host)
    {
        Assert.IsTrue(host.IsAcadFamily());
    }

    [TestMethod]
    [DataRow(HostApp.Revit)]
    [DataRow(HostApp.Navisworks)]
    [DataRow(HostApp.Rhino)]
    [DataRow(HostApp.Tekla)]
    public void IsAcadFamily_is_false_outside_autocad_family(HostApp host)
    {
        Assert.IsFalse(host.IsAcadFamily());
    }
}
