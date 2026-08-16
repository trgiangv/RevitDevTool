using DevTools.Hosting;
using DevTools.Hosting.Revit;

namespace DevTools.Hosting.Revit.Tests;

public sealed class RevitStartupDialogSpecTests
{
    [Fact]
    public void Catalog_is_unsigned_add_in_only_with_closed_blocked_pair()
    {
        var options = new RevitStartupDialogSpec().CreateOptions();
        Assert.Equal(["unsigned add-in"], options.DialogTitleKeywords);
        Assert.Equal(["always load"], options.PreferredButtonKeywords);
        Assert.Equal(["do not load", "load once"], options.BlockedButtonKeywords);
        Assert.DoesNotContain("questionable add-in", options.DialogTitleKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("cancel", options.BlockedButtonKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("no", options.BlockedButtonKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("#32770", options.WindowClassName);
        Assert.Equal("button", options.ButtonClassName);
    }
}
