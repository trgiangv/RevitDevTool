using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using static DevTools.ModelsContext.Tests.TestModel;

namespace DevTools.ModelsContext.Tests.Explorer;

[TestClass]
public sealed class CategoryNestingTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;

    [TestMethod]
    public async Task ChildListsUnderParent()
    {
        using var fixture = await PanelFixture.OpenAsync(WithToposolid());
        var items = fixture.Explorer.Categories.Picker.Items.ToList();

        var parent = items.First(item => item.Name == "Toposolid");
        var child = items.First(item => item.Name == SubDivisionName);

        Assert.AreSame(parent, child.Parent);
        Assert.AreEqual(items.IndexOf(parent) + 1, items.IndexOf(child));
        Assert.AreEqual(SubDivisionCount, child.Count);
        Assert.AreEqual(ToposolidCount + SubDivisionCount, parent.Count);
    }

    [TestMethod]
    public async Task ChildHiddenUntilExpanded()
    {
        using var fixture = await PanelFixture.OpenAsync(WithToposolid());
        var picker = fixture.Explorer.Categories.Picker;
        var parent = picker.Items.First(item => item.Name == "Toposolid");

        Assert.DoesNotContain(SubDivisionName, picker.VisibleItems.Select(item => item.Name).ToList());
        parent.IsExpanded = true;

        Assert.Contains(SubDivisionName, picker.VisibleItems.Select(item => item.Name).ToList());
    }

    [TestMethod]
    public async Task BoxCheckCascades()
    {
        var (fixture, picker, parent, child) = await OpenNoneAsync();
        using var _ = fixture;

        parent.BoxState = true;
        Assert.IsTrue(child.IsChecked);

        parent.BoxState = false;
        Assert.IsFalse(child.IsChecked);
        Assert.IsFalse(parent.IsChecked);
    }

    [TestMethod]
    public async Task RowCheckChecksChildren()
    {
        var (fixture, _, parent, child) = await OpenNoneAsync();
        using var _ = fixture;

        parent.IsChecked = true;
        Assert.IsTrue(child.IsChecked);

        parent.IsChecked = false;
        Assert.IsFalse(child.IsChecked);
    }

    [TestMethod]
    public async Task ChildrenDriveParent()
    {
        var (fixture, _, parent, child) = await OpenNoneAsync();
        using var _ = fixture;

        child.IsChecked = true;
        Assert.IsTrue(parent.IsChecked);
        Assert.IsTrue(parent.BoxState);

        child.IsChecked = false;
        Assert.IsFalse(parent.IsChecked);
    }

    [TestMethod]
    public async Task PartlyClickChecksAll()
    {
        var (fixture, picker, parent, child) = await OpenNoneAsync();
        using var _ = fixture;
        picker.SetChecks([parent], [child]);
        Assert.IsNull(parent.BoxState);

        parent.BoxState = false;

        Assert.IsTrue(parent.IsChecked);
        Assert.IsTrue(child.IsChecked);
    }

    [TestMethod]
    public async Task SearchOpenIsTemporary()
    {
        using var fixture = await PanelFixture.OpenAsync(WithToposolid());
        var picker = fixture.Explorer.Categories.Picker;
        picker.Search = "sub-div";

        picker.Search = string.Empty;

        Assert.IsFalse(picker.Items.First(item => item.Name == "Toposolid").IsExpanded);
        Assert.DoesNotContain(SubDivisionName, picker.VisibleItems.Select(item => item.Name).ToList());
    }

    [TestMethod]
    public async Task HideKeepsParentOfCheckedChild()
    {
        var (fixture, picker, parent, child) = await OpenNoneAsync();
        using var _ = fixture;
        picker.SetChecks([child], [parent]);

        picker.HideUnchecked = true;
        parent.IsExpanded = true;

        CollectionAssert.AreEqual(new[] { "Toposolid", SubDivisionName }, picker.VisibleItems.Select(item => item.Name).ToArray());
    }

    private static async Task<(PanelFixture Fixture, CategoryPickerViewModel Picker, CategoryItemViewModel Parent,
        CategoryItemViewModel Child)> OpenNoneAsync()
    {
        var fixture = await PanelFixture.OpenAsync(WithToposolid());
        var picker = fixture.Explorer.Categories.Picker;
        picker.CheckNoneCommand.Execute(null);
        return (fixture, picker, picker.Items.First(item => item.Name == "Toposolid"),
            picker.Items.First(item => item.Name == SubDivisionName));
    }
    [TestMethod]
    public async Task AllChecksCollapsedChildren()
    {
        using var fixture = await PanelFixture.OpenAsync(WithToposolid());
        var picker = fixture.Explorer.Categories.Picker;
        picker.CheckNoneCommand.Execute(null);

        picker.CheckAllCommand.Execute(null);

        Assert.IsTrue(picker.Items.All(item => item.IsChecked));
    }

    [TestMethod]
    public async Task ChildSearchExpandsParent()
    {
        using var fixture = await PanelFixture.OpenAsync(WithToposolid());
        var picker = fixture.Explorer.Categories.Picker;

        picker.Search = "sub-div";

        CollectionAssert.AreEqual(new[] { "Toposolid", SubDivisionName }, picker.VisibleItems.Select(item => item.Name).ToArray());
        Assert.IsTrue(picker.Items.First(item => item.Name == "Toposolid").IsExpanded);
    }

    [TestMethod]
    public async Task SubgroupRowNestsUnderParent()
    {
        using var fixture = await PanelFixture.OpenAsync(WithToposolid());

        var parent = fixture.Group(Current, "Toposolid");

        Assert.IsFalse(fixture.Explorer.Rows.Any(row => row.Text == SubDivisionName), "The subgroup row is top-level.");
        Assert.AreEqual(SubDivisionName, parent.Subgroups!.Single().Text);
        Assert.AreEqual(ToposolidCount + SubDivisionCount, parent.Count);
    }

    [TestMethod]
    public async Task ExpandShowsSubgroupFirst()
    {
        using var fixture = await PanelFixture.OpenAsync(WithToposolid());
        var parent = fixture.Group(Current, "Toposolid");

        await fixture.Explorer.ExpandAsync(parent);

        var index = fixture.Explorer.Rows.IndexOf(parent);
        Assert.AreEqual(SubDivisionName, fixture.Explorer.Rows[index + 1].Text);
        Assert.AreEqual(parent.Level + 1, fixture.Explorer.Rows[index + 1].Level);
    }

    [TestMethod]
    public void MissingParentGetsEmptyRow()
    {
        var snapshot = FakeModelGenerator.BuildSnapshot(WithToposolid().SourceOf(Current));
        var groups = new[] { new GroupCount(SubDivisions, SubDivisionName, SubDivisionCount, Toposolid) };

        var result = CategoryTree.WithParents(groups, [snapshot]);

        Assert.AreEqual(new GroupCount(Toposolid, "Toposolid", 0), result.Single(group => group.GroupId == Toposolid));
    }
}
