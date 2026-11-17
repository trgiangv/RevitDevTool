using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class CustomFilterLibraryTests
{
    private static readonly CustomFilter Doors = Filter("Doors");
    private static readonly CustomFilter Walls = Filter("Walls");

    [TestMethod]
    public void UpsertAddsNewFilter()
    {
        var saved = CustomFilterLibrary.Upsert([Doors], null, Walls);

        CollectionAssert.AreEqual(new[] { Doors, Walls }, saved);
    }

    [TestMethod]
    public void UpsertReplacesRenamedFilter()
    {
        var renamed = Filter("Exterior walls");

        var saved = CustomFilterLibrary.Upsert([Doors, Walls], Filter("WALLS"), renamed);

        CollectionAssert.AreEqual(new[] { Doors, renamed }, saved);
    }

    [TestMethod]
    public void UpsertAddsWhenPreviousMissing()
    {
        var saved = CustomFilterLibrary.Upsert([Doors], Filter("Gone"), Walls);

        CollectionAssert.AreEqual(new[] { Doors, Walls }, saved);
    }

    [TestMethod]
    public void RemoveIgnoresCase()
    {
        var saved = CustomFilterLibrary.Remove([Doors, Walls], "doors");

        CollectionAssert.AreEqual(new[] { Walls }, saved);
    }

    [TestMethod]
    public void StripDropsUnsupportedRules()
    {
        var rule = new FilterRule(FakeParameters.Mark, FilterOperator.Equal, "m-001");
        var definition = new FilterDefinition([7], FilterSet.Any(
            rule,
            new UnsupportedRule("global parameter"),
            FilterSet.All(new UnsupportedRule("other"), rule)));

        var stripped = CustomFilterLibrary.Strip(definition);

        CollectionAssert.AreEqual(new long[] { 7 }, stripped.Groups.ToArray());
        Assert.AreEqual(SetMatch.Any, stripped.Root.Match);
        Assert.HasCount(2, stripped.Root.Children);
        Assert.AreSame(rule, stripped.Root.Children[0]);
        CollectionAssert.AreEqual(new FilterNode[] { rule }, ((FilterSet)stripped.Root.Children[1]).Children.ToArray());
    }

    private static CustomFilter Filter(string name)
    {
        return new CustomFilter(name, FilterDefinition.Empty);
    }
}
