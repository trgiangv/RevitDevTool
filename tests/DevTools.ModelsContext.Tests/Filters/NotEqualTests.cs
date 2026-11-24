using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests.Filters;

[TestClass]
public sealed class NotEqualTests
{
    [TestMethod]
    public async Task EmptyTextDoesNotEqual()
    {
        var model = new FakeModel(TestModel.SmallData);

        var matched = await MatchAsync(model, FakeModelGenerator.Current, FakeParameters.Comments, "Check");

        AssertEmptiesMatch(model, FakeModelGenerator.Current, FakeParameters.Comments, matched);
    }

    [TestMethod]
    public async Task EmptyNumberDoesNotEqual()
    {
        var model = new FakeModel(TestModel.SmallData);

        var matched = await MatchAsync(model, FakeModelGenerator.ArchFirst, FakeParameters.SwingAngle, "45");

        AssertEmptiesMatch(model, FakeModelGenerator.ArchFirst, FakeParameters.SwingAngle, matched);
    }

    [TestMethod]
    public async Task NoneReferenceDoesNotEqual()
    {
        var levels = TestModel.SmallData.Levels.Where(pair => pair.Value != "Level 0")
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var model = new FakeModel(TestModel.SmallData with { Levels = levels });

        var matched = await MatchAsync(model, FakeModelGenerator.Current, FakeParameters.Level, "Level 1");

        AssertEmptiesMatch(model, FakeModelGenerator.Current, FakeParameters.Level, matched);
    }

    private static async Task<HashSet<long>> MatchAsync(FakeModel model, DocumentKey document, ParameterKey key, string value)
    {
        var definition = new FilterDefinition([], FilterSet.All(new FilterRule(key, FilterOperator.NotEqual, value)));
        return [.. await model.EvaluateAsync(document, definition, null, CancellationToken.None)];
    }

    private static void AssertEmptiesMatch(FakeModel model, DocumentKey document, ParameterKey key, HashSet<long> matched)
    {
        var snapshot = model.SnapshotOf(document);
        var empties = snapshot.Rows
            .Where(row => FakeParameters.IdOf(snapshot, row, key) is not null
                          && FakeParameters.TryGet(snapshot, row, key, model.Data.Levels, out var text)
                          && text.Length == 0)
            .Select(row => row.Id)
            .ToList();

        Assert.IsNotEmpty(empties);
        Assert.IsTrue(empties.All(matched.Contains));
    }
}
