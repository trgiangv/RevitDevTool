using DevTools.Hosting;
using DevTools.Hosting.Revit;

namespace DevTools.Hosting.Revit.Tests;

[TestClass]
public sealed class RevitStartupDialogSpecTests
{
    [TestMethod]
    public void Catalog_is_unsigned_add_in_only_with_closed_blocked_pair()
    {
        var options = new RevitStartupDialogSpec().CreateOptions();
        CollectionAssert.AreEqual(new[] { "unsigned add-in" }, options.DialogTitleKeywords.ToArray());
        CollectionAssert.AreEqual(new[] { "always load" }, options.PreferredButtonKeywords.ToArray());
        CollectionAssert.AreEqual(new[] { "do not load", "load once" }, options.BlockedButtonKeywords.ToArray());
        Assert.DoesNotContain("questionable add-in", options.DialogTitleKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("cancel", options.BlockedButtonKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("no", options.BlockedButtonKeywords, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual("#32770", options.WindowClassName);
        Assert.AreEqual("button", options.ButtonClassName);
    }
}
