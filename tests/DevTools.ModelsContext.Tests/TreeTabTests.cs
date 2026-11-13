using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;
using System.Collections.Specialized;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class TreeTabTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;

    [TestMethod]
    public async Task TreeExpandInsertsChildren()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");

        await fixture.Tree.ExpandAsync(ducts);

        var index = fixture.Tree.Rows.IndexOf(ducts);
        Assert.IsNotEmpty(ducts.Children!);
        CollectionAssert.AreEqual(ducts.Children!.ToList(),
            fixture.Tree.Rows.Skip(index + 1).Take(ducts.Children!.Count).ToList());
    }

    [TestMethod]
    public async Task ExpandRaisesOneReset()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        var events = new List<NotifyCollectionChangedAction>();
        fixture.Tree.Rows.CollectionChanged += (_, args) => events.Add(args.Action);

        await fixture.Tree.ExpandAsync(ducts);

        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Reset }, events);
    }

    [TestMethod]
    public async Task CollapseRemovesDeeperRows()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");
        var before = fixture.Tree.Rows.Count;
        await fixture.Tree.ExpandAsync(ducts);

        fixture.Tree.Collapse(ducts);

        Assert.HasCount(before, fixture.Tree.Rows);
    }

    [TestMethod]
    public async Task InstancesPageAtTwoHundred()
    {
        using var fixture = await PanelFixture.OpenAsync(Large.Value);
        var type = await LargestTypeAsync(fixture);

        await fixture.Tree.ExpandAsync(type);

        Assert.HasCount(TreeTabViewModel.InstancePageSize + 1, type.Children!);
        var more = type.Children![TreeTabViewModel.InstancePageSize];
        Assert.AreEqual(TreeRowKind.MorePage, more.Kind);
        StringAssert.StartsWith(more.Text, $"{TreeTabViewModel.InstancePageSize} of ");
    }

    [TestMethod]
    public async Task MoreLoadsNextPage()
    {
        using var fixture = await PanelFixture.OpenAsync(Large.Value);
        var type = await LargestTypeAsync(fixture);
        await fixture.Tree.ExpandAsync(type);

        await fixture.Tree.LoadMoreCommand.ExecuteAsync(type.Children![TreeTabViewModel.InstancePageSize]);

        Assert.IsGreaterThan(TreeTabViewModel.InstancePageSize + 1, type.Children!.Count);
        Assert.IsTrue(fixture.Tree.Rows.Contains(type.Children[TreeTabViewModel.InstancePageSize]));
    }

    [TestMethod]
    public async Task SearchMatchesRowText()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Tree.SearchText = "duct fittings";
        fixture.Settle();

        Assert.IsTrue(fixture.Tree.Rows.Any(row => row.Kind == TreeRowKind.Group && row.Text == "Duct Fittings"));
        Assert.IsFalse(fixture.Tree.Rows.Any(row => row.Text == "Walls"));
    }

    [TestMethod]
    public async Task SearchFindsElementId()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var id = fixture.Model.Data.SourceOf(Current).Elements[1234].Id;

        fixture.Tree.SearchText = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fixture.Settle();

        Assert.IsTrue(fixture.Tree.Rows.Any(row => row.Kind == TreeRowKind.Instance && row.Id == id));
    }

    [TestMethod]
    public async Task AllModelsGroupOnTop()
    {
        using var fixture = await PanelFixture.OpenAsync();

        var first = fixture.Tree.Rows[0];

        Assert.AreEqual(TreeRowKind.AllModelsHeader, first.Kind);
        Assert.AreEqual(TreeRowKind.AllModelsGroup, fixture.Tree.Rows[1].Kind);
    }

    [TestMethod]
    public async Task EyeHidesThenShowsRow()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var ducts = fixture.Group(Current, "Ducts");

        await fixture.Tree.ToggleEyeCommand.ExecuteAsync(ducts);
        var hidden = fixture.Group(Current, "Ducts").Eye;
        await fixture.Tree.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));

        Assert.AreEqual(RowEye.Hidden, hidden);
        Assert.AreEqual(RowEye.Shown, fixture.Group(Current, "Ducts").Eye);
        Assert.AreEqual(1, fixture.Model.ResetCount);
    }

    [TestMethod]
    public async Task HiddenAllModelsRowDisablesOthers()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var row = fixture.Tree.Rows.First(row => row.Kind == TreeRowKind.AllModelsGroup && row.Text == "Ducts");

        await fixture.Tree.ToggleEyeCommand.ExecuteAsync(row);

        Assert.AreEqual(RowEye.HiddenByAllModels, fixture.Group(Current, "Ducts").Eye);
    }

    private static readonly Lazy<FakeModelData> Large = new(() => FakeModelGenerator.Generate(new FakeModelOptions
    {
        CurrentElements = 20_000,
        LinkElements = 100
    }));

    private static async Task<TreeRow> LargestTypeAsync(PanelFixture fixture)
    {
        var snapshot = FakeModelGenerator.BuildSnapshot(fixture.Model.Data.SourceOf(Current));
        var (group, family, type) = snapshot.Counts.Children.Values
            .SelectMany(groupNode => groupNode.Children.Values.SelectMany(familyNode => familyNode.Children.Values
                .Select(typeNode => (groupNode, familyNode, typeNode))))
            .OrderByDescending(path => path.typeNode.Count)
            .First();
        Assert.IsGreaterThan(TreeTabViewModel.InstancePageSize, type.Count);

        var groupRow = fixture.Group(Current, snapshot.GroupName(group.Key));
        await fixture.Tree.ExpandAsync(groupRow);
        var familyRow = groupRow.Children!.First(row => row.Text == snapshot.Names[family.Key]);
        await fixture.Tree.ExpandAsync(familyRow);
        return familyRow.Children!.First(row => row.Text == snapshot.TypeName(type.Key));
    }
}
