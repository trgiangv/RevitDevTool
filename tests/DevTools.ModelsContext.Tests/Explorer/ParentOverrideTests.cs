using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;
using static DevTools.ModelsContext.Tests.TestModel;

namespace DevTools.ModelsContext.Tests.Explorer;

[TestClass]
public sealed class ParentOverrideTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;
    private static readonly RgbColor Red = new(255, 0, 0);
    private static readonly RgbColor Blue = new(0, 0, 255);

    [TestMethod]
    public async Task ParentColorCoversChildren()
    {
        using var fixture = await OpenAsync(Current);

        await ColorAsync(fixture, Current, "Toposolid", Blue);

        Assert.AreEqual(Blue, fixture.Group(Current, SubDivisionName).OverrideColor);
        Assert.HasCount(ToposolidCount + SubDivisionCount, ((ElementsStep)LastStep(fixture)).Ids);
    }

    [TestMethod]
    public async Task ParentColorReplacesChildColor()
    {
        using var fixture = await OpenAsync(Current);
        await ColorAsync(fixture, Current, SubDivisionName, Red);

        await ColorAsync(fixture, Current, "Toposolid", Blue);

        Assert.AreEqual(Blue, fixture.Group(Current, SubDivisionName).OverrideColor);
        Assert.AreEqual(Blue, fixture.Group(Current, "Toposolid").OverrideColor);
    }

    [TestMethod]
    public async Task LaterChildColorDiffers()
    {
        using var fixture = await OpenAsync(Current);
        await ColorAsync(fixture, Current, "Toposolid", Blue);

        await ColorAsync(fixture, Current, SubDivisionName, Red);

        Assert.AreEqual(Red, fixture.Group(Current, SubDivisionName).OverrideColor);
        Assert.AreEqual(Blue, fixture.Group(Current, "Toposolid").OverrideColor);
    }

    [TestMethod]
    public async Task ResetClearsParentAndChild()
    {
        using var fixture = await OpenAsync(Current);
        await ColorAsync(fixture, Current, SubDivisionName, Red);
        await ColorAsync(fixture, Current, "Toposolid", Blue);

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        Assert.IsNull(fixture.Group(Current, SubDivisionName).OverrideColor);
        Assert.IsNull(fixture.Group(Current, "Toposolid").OverrideColor);
    }

    [TestMethod]
    public async Task ParentHalftoneCoversChildren()
    {
        using var fixture = await OpenAsync(Current);

        await fixture.Explorer.RunToolAsync(fixture.Group(Current, "Toposolid"), MenuAction.Halftone);

        Assert.IsTrue(fixture.Group(Current, SubDivisionName).IsHalftone);
    }

    [TestMethod]
    public async Task ParentHideCoversChildren()
    {
        using var fixture = await OpenAsync(Current);

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Toposolid"));

        Assert.HasCount(ToposolidCount + SubDivisionCount, ((ElementsStep)LastStep(fixture)).Ids);
    }

    [TestMethod]
    public async Task LinkParentColorUsesAllCategories()
    {
        using var fixture = await OpenAsync(Mep);

        await ColorAsync(fixture, Mep, "Toposolid", Blue);

        var step = (LinkFilterStep)LastStep(fixture);
        CollectionAssert.AreEquivalent(new[] { Toposolid, SubDivisions }, step.Filter.Groups.ToList());
        Assert.HasCount(ToposolidCount + SubDivisionCount, step.Ids);
        Assert.AreEqual(Blue, fixture.Group(Mep, SubDivisionName).OverrideColor);
    }

    [TestMethod]
    public async Task LinkChildColorUsesOwnCategory()
    {
        using var fixture = await OpenAsync(Mep);

        await ColorAsync(fixture, Mep, SubDivisionName, Red);

        var step = (LinkFilterStep)LastStep(fixture);
        CollectionAssert.AreEqual(new[] { SubDivisions }, step.Filter.Groups.ToList());
        Assert.IsNull(fixture.Group(Mep, "Toposolid").OverrideColor);
    }

    [TestMethod]
    public async Task LinkParentReplacesChildColor()
    {
        using var fixture = await OpenAsync(Mep);
        await ColorAsync(fixture, Mep, SubDivisionName, Red);

        await ColorAsync(fixture, Mep, "Toposolid", Blue);

        Assert.AreEqual(Blue, fixture.Group(Mep, SubDivisionName).OverrideColor);
    }

    [TestMethod]
    public async Task LinkLaterChildColorDiffers()
    {
        using var fixture = await OpenAsync(Mep);
        await ColorAsync(fixture, Mep, "Toposolid", Blue);

        await ColorAsync(fixture, Mep, SubDivisionName, Red);

        Assert.AreEqual(Red, fixture.Group(Mep, SubDivisionName).OverrideColor);
        Assert.AreEqual(Blue, fixture.Group(Mep, "Toposolid").OverrideColor);
    }

    [TestMethod]
    public async Task LinkResetClearsBoth()
    {
        using var fixture = await OpenAsync(Mep);
        await ColorAsync(fixture, Mep, "Toposolid", Blue);
        await ColorAsync(fixture, Mep, SubDivisionName, Red);

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        Assert.IsNull(fixture.Group(Mep, SubDivisionName).OverrideColor);
        Assert.IsNull(fixture.Group(Mep, "Toposolid").OverrideColor);
    }

    [TestMethod]
    public async Task LinkParentHalftoneCoversChildren()
    {
        using var fixture = await OpenAsync(Mep);

        await fixture.Explorer.RunToolAsync(fixture.Group(Mep, "Toposolid"), MenuAction.Halftone);

        Assert.IsTrue(fixture.Group(Mep, SubDivisionName).IsHalftone);
    }

    [TestMethod]
    public async Task BoxParentColorCoversChildren()
    {
        using var fixture = await OpenAsync(Current);
        var picker = fixture.Explorer.Categories.Picker;
        picker.CheckNoneCommand.Execute(null);
        picker.Items.First(item => item.Name == "Toposolid").IsChecked = true;

        await fixture.Explorer.Categories.ColorCommand.ExecuteAsync(null);
        await fixture.Panel.ContextMenu.ApplyColorCommand.ExecuteAsync(Blue);

        var ids = fixture.Actions.ExecutedPlans.Single().Steps.Cast<CategoryStep>().Select(step => step.CategoryId);
        CollectionAssert.AreEquivalent(new[] { Toposolid, SubDivisions }, ids.ToList());
    }

    [TestMethod]
    public async Task ChildShowWaitsForParent()
    {
        using var fixture = await OpenAsync(Current);
        var picker = fixture.Explorer.Categories.Picker;
        await picker.Items.First(item => item.Name == "Toposolid").ToggleEyeCommand.ExecuteAsync(null);

        await picker.Items.First(item => item.Name == SubDivisionName).ToggleEyeCommand.ExecuteAsync(null);

        Assert.AreEqual(ExplorerCategories.ParentHiddenReason, fixture.Panel.Message);
        Assert.HasCount(1, fixture.Actions.ExecutedPlans);
    }

    private static async Task<PanelFixture> OpenAsync(DocumentKey document)
    {
        var fixture = await PanelFixture.OpenAsync(WithToposolid());
        if (!document.IsCurrentModel)
        {
            fixture.Panel.ContextBar.Check([document]);
        }

        return fixture;
    }

    private static async Task ColorAsync(PanelFixture fixture, DocumentKey document, string group, RgbColor color)
    {
        await fixture.Explorer.RunToolAsync(fixture.Group(document, group), MenuAction.Color);
        await fixture.Panel.ContextMenu.ApplyColorCommand.ExecuteAsync(color);
    }

    private static PlanStep LastStep(PanelFixture fixture)
    {
        return fixture.Actions.ExecutedPlans[^1].Steps.Single();
    }
}
