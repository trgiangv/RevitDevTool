using DevTools.Hosting;
using DevTools.Hosting.Acad;

namespace DevTools.Hosting.Acad.Tests;

public sealed class AcadStartupDialogSpecTests
{
    [Fact]
    public void Catalog_is_unsigned_executable_file_only_with_closed_blocked_pair()
    {
        var options = new AcadStartupDialogSpec().CreateOptions();
        Assert.Equal(["unsigned executable file"], options.DialogTitleKeywords);
        Assert.Equal(["always load"], options.PreferredButtonKeywords);
        Assert.Equal(["do not load", "load once"], options.BlockedButtonKeywords);
        Assert.DoesNotContain("unsigned add-in", options.DialogTitleKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("questionable add-in", options.DialogTitleKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("#32770", options.WindowClassName);
        Assert.Equal("button", options.ButtonClassName);
    }

    [Fact]
    public void Supports_all_autocad_family_hosts()
    {
        var spec = new AcadStartupDialogSpec();
        foreach (var host in Enum.GetValues<HostApp>().Where(h => h.IsAcadFamily()))
            Assert.True(spec.Supports(host));
        Assert.False(spec.Supports(HostApp.Revit));
    }

    [Fact]
    public void ProductIdMap_keeps_known_family_ids()
    {
        Assert.Equal(HostApp.Civil3D, AcadPathResolver.ProductIdMap["00"]);
        Assert.Equal(HostApp.AutoCad, AcadPathResolver.ProductIdMap["01"]);
        Assert.Equal(HostApp.AcadMap3D, AcadPathResolver.ProductIdMap["02"]);
        Assert.Equal(HostApp.AcadArch, AcadPathResolver.ProductIdMap["04"]);
        Assert.Equal(HostApp.AcadMech, AcadPathResolver.ProductIdMap["05"]);
        Assert.Equal(HostApp.AcadMep, AcadPathResolver.ProductIdMap["06"]);
        Assert.Equal(HostApp.AcadElec, AcadPathResolver.ProductIdMap["07"]);
        Assert.Equal(HostApp.Plant3D, AcadPathResolver.ProductIdMap["17"]);
    }
}
