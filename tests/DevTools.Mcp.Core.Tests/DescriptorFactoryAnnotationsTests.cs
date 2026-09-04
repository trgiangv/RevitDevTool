using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Core.Tests;

public sealed class DescriptorFactoryAnnotationsTests
{
    [Fact]
    public void BuildToolAnnotations_AllNull_ReturnsNull()
    {
        Assert.Null(DescriptorFactory.BuildToolAnnotations(null));
        Assert.Null(DescriptorFactory.BuildToolAnnotations("  "));
    }

    [Fact]
    public void BuildToolAnnotations_WithHints_ReturnsAnnotations()
    {
        var annotations = DescriptorFactory.BuildToolAnnotations(
            "Demo",
            readOnly: true,
            destructive: false,
            idempotent: true,
            openWorld: false);

        Assert.NotNull(annotations);
        Assert.Equal("Demo", annotations!.Title);
        Assert.True(annotations.ReadOnlyHint);
        Assert.False(annotations.DestructiveHint);
        Assert.True(annotations.IdempotentHint);
        Assert.False(annotations.OpenWorldHint);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseIcons_BlankSource_ReturnsNull(string? iconSource)
    {
        Assert.Null(DescriptorFactory.ParseIcons(iconSource));
    }

    [Fact]
    public void ParseIcons_TrimsSource()
    {
        var icons = DescriptorFactory.ParseIcons("  https://example/icon.png  ");

        var icon = Assert.Single(icons!);
        Assert.Equal("https://example/icon.png", icon.Source);
    }
}
