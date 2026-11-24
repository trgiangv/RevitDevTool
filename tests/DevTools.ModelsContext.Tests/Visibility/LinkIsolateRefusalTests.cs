using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Visibility;

[TestClass]
public sealed class LinkIsolateRefusalTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;

    public static IEnumerable<object[]> Refusals =>
    [
        [VisibilityText.RevealOn],
        [VisibilityText.NoLinkView],
        [VisibilityText.LinkIsolateTooMany]
    ];

    [TestMethod]
    [DynamicData(nameof(Refusals))]
    public async Task RefusedIsolateChangesNothing(string refusal)
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Current, Mep]);
        fixture.Actions.Refusal = refusal;
        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));

        await fixture.Panel.ContextMenu[MenuAction.Isolate].Command.ExecuteAsync(null);

        Assert.AreEqual(refusal, fixture.Panel.Message);
        Assert.AreEqual(JournalPage.Empty, fixture.Panel.Journal);
        Assert.AreEqual(RowEye.Shown, fixture.Group(Mep, "Pipes").Eye);
        Assert.AreEqual(RowEye.Shown, fixture.Group(Current, "Walls").Eye);
    }

    [TestMethod]
    public void TooManyNamesTheCap()
    {
        StringAssert.Contains(VisibilityText.LinkIsolateTooMany, LinkRules.MaxIsolateRest.ToString("N0"));
    }
}
