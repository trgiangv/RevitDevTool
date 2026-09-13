using DevTools.Hosting;
using DevTools.Hosting.Acad;

namespace DevTools.Hosting.Acad.Tests;

[TestClass]
public sealed class AcadStartupDialogSpecTests
{
    [TestMethod]
    public void Catalog_is_unsigned_executable_file_only_with_closed_blocked_pair()
    {
        var options = new AcadStartupDialogSpec().CreateOptions();
        CollectionAssert.AreEqual(new[] { "unsigned executable file" }, options.DialogTitleKeywords.ToArray());
        CollectionAssert.AreEqual(new[] { "always load" }, options.PreferredButtonKeywords.ToArray());
        CollectionAssert.AreEqual(new[] { "do not load", "load once" }, options.BlockedButtonKeywords.ToArray());
        Assert.DoesNotContain("unsigned add-in", options.DialogTitleKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("questionable add-in", options.DialogTitleKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual("#32770", options.WindowClassName);
        Assert.AreEqual("button", options.ButtonClassName);
    }

    [TestMethod]
    public void Supports_all_autocad_family_hosts()
    {
        var spec = new AcadStartupDialogSpec();
        foreach (var host in Enum.GetValues<HostApp>().Where(h => h.IsAcadFamily()))
            Assert.IsTrue(spec.Supports(host));
        Assert.IsFalse(spec.Supports(HostApp.Revit));
    }

    [TestMethod]
    public void ProductIdMap_keeps_known_family_ids()
    {
        Assert.AreEqual(HostApp.Civil3D, AcadPathResolver.ProductIdMap["00"]);
        Assert.AreEqual(HostApp.AutoCad, AcadPathResolver.ProductIdMap["01"]);
        Assert.AreEqual(HostApp.AcadMap3D, AcadPathResolver.ProductIdMap["02"]);
        Assert.AreEqual(HostApp.AcadArch, AcadPathResolver.ProductIdMap["04"]);
        Assert.AreEqual(HostApp.AcadMech, AcadPathResolver.ProductIdMap["05"]);
        Assert.AreEqual(HostApp.AcadMep, AcadPathResolver.ProductIdMap["06"]);
        Assert.AreEqual(HostApp.AcadElec, AcadPathResolver.ProductIdMap["07"]);
        Assert.AreEqual(HostApp.Plant3D, AcadPathResolver.ProductIdMap["17"]);
    }
}
