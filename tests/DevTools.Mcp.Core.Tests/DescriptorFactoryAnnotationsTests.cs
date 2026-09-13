using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

[TestClass]
public sealed class DescriptorFactoryAnnotationsTests
{
    [TestMethod]
    public void BuildToolAnnotations_AllNull_ReturnsNull()
    {
        Assert.IsNull(DescriptorFactory.BuildToolAnnotations(null));
        Assert.IsNull(DescriptorFactory.BuildToolAnnotations("  "));
    }

    [TestMethod]
    public void BuildToolAnnotations_WithHints_ReturnsAnnotations()
    {
        var annotations = DescriptorFactory.BuildToolAnnotations(
            "Demo",
            readOnly: true,
            destructive: false,
            idempotent: true,
            openWorld: false);

        Assert.IsNotNull(annotations);
        Assert.AreEqual("Demo", annotations!.Title);
        Assert.IsTrue(annotations.ReadOnlyHint);
        Assert.IsFalse(annotations.DestructiveHint);
        Assert.IsTrue(annotations.IdempotentHint);
        Assert.IsFalse(annotations.OpenWorldHint);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ParseIcons_BlankSource_ReturnsNull(string? iconSource)
    {
        Assert.IsNull(DescriptorFactory.ParseIcons(iconSource));
    }

    [TestMethod]
    public void ParseIcons_TrimsSource()
    {
        var icons = DescriptorFactory.ParseIcons("  https://example/icon.png  ");

        var icon = icons!.Single();
        Assert.AreEqual("https://example/icon.png", icon.Source);
    }
}
