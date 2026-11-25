using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests.Panel;

[TestClass]
public sealed class MessageTests
{
    private const string Refusal = "Revit refused.";

    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;

    [TestMethod]
    public async Task RefusalStaysInSameContext()
    {
        using var fixture = await RefusedAsync();

        fixture.Settle();

        Assert.AreEqual(Refusal, fixture.Panel.Message);
    }

    [TestMethod]
    public async Task ScopeChangeClearsMessage()
    {
        using var fixture = await RefusedAsync();

        var bar = fixture.Panel.ContextBar;
        bar.Scope = bar.Scope == ScopeKind.WholeModel ? ScopeKind.ActiveView : ScopeKind.WholeModel;

        Assert.IsNull(fixture.Panel.Message);
    }

    [TestMethod]
    public async Task CheckChangeClearsMessage()
    {
        using var fixture = await RefusedAsync();

        fixture.Panel.ContextBar.Check([Current, Mep]);

        Assert.IsNull(fixture.Panel.Message);
    }

    [TestMethod]
    public async Task TabChangeClearsMessage()
    {
        using var fixture = await RefusedAsync();

        fixture.Panel.SelectedTab = PanelTab.Filter;

        Assert.IsNull(fixture.Panel.Message);
    }

    [TestMethod]
    public async Task ViewSwitchClearsMessage()
    {
        using var fixture = await RefusedAsync();

        fixture.Model.View = new ViewInfo(Current, "view-2", "Level 2", ViewKind.Plan);
        fixture.Model.RaiseViewActivated(fixture.Model.View);
        fixture.Settle();

        Assert.IsNull(fixture.Panel.Message);
    }

    private static async Task<PanelFixture> RefusedAsync()
    {
        var fixture = await PanelFixture.OpenAsync();
        fixture.Actions.Refusal = Refusal;
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));
        fixture.Actions.Refusal = null;
        Assert.AreEqual(Refusal, fixture.Panel.Message);
        return fixture;
    }
}
