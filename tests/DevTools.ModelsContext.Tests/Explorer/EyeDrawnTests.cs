using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests.Explorer;

[TestClass]
public sealed class EyeDrawnTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;

    [TestMethod]
    public async Task LinkHideIgnoresUndrawn()
    {
        using var fixture = await WholeModelAsync();
        MarkHalfUndrawn(fixture, Mep, "Ducts");

        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        Assert.AreEqual(HideState.Hidden, fixture.Group(Mep, "Ducts").HideState);
    }

    [TestMethod]
    public async Task CurrentHideIgnoresUndrawn()
    {
        using var fixture = await WholeModelAsync();
        MarkHalfUndrawn(fixture, Current, "Walls");

        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Current, "Walls"));

        Assert.AreEqual(HideState.Hidden, fixture.Group(Current, "Walls").HideState);
    }

    [TestMethod]
    public async Task HiddenEyeShowsAgain()
    {
        using var fixture = await WholeModelAsync();
        MarkHalfUndrawn(fixture, Mep, "Ducts");
        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        Assert.AreEqual(HideState.Shown, fixture.Group(Mep, "Ducts").HideState);
    }

    [TestMethod]
    public async Task ResetForgetsUndrawn()
    {
        using var fixture = await WholeModelAsync();
        MarkHalfUndrawn(fixture, Mep, "Ducts");
        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        Assert.AreEqual(HideState.Shown, fixture.Group(Mep, "Ducts").HideState);
        Assert.IsEmpty(fixture.Panel.Journal.LinkHides);
    }

    private static async Task<PanelFixture> WholeModelAsync()
    {
        var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Scope = ScopeKind.WholeModel;
        fixture.Panel.ContextBar.Check([Current, Mep]);
        fixture.Settle();
        return fixture;
    }

    private static void MarkHalfUndrawn(PanelFixture fixture, DocumentKey document, string group)
    {
        var elements = fixture.Explorer.ElementsOf([fixture.Group(document, group)]).ToList();
        Assert.IsGreaterThan(1, elements.Count);
        fixture.Model.ViewNotDrawn.UnionWith(elements.Take(elements.Count / 2));
    }
}
