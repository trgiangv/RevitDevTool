using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests.Filters;

[TestClass]
public sealed class CaseSensitiveTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;

    [TestMethod]
    public async Task CaseEqualsIsExact()
    {
        var sensitive = await CountAsync(FilterSet.All(Mark(FilterOperator.Equal, "m-001", true)));
        var insensitive = await CountAsync(FilterSet.All(Mark(FilterOperator.Equal, "m-001", false)));

        Assert.AreEqual(0, sensitive);
        Assert.IsGreaterThan(0, insensitive);
    }

    [TestMethod]
    public async Task CaseNotEqualsKeepsOthers()
    {
        var total = await CountAsync(FilterSet.All());

        var sensitive = await CountAsync(FilterSet.All(Mark(FilterOperator.NotEqual, "m-001", true)));
        var insensitive = await CountAsync(FilterSet.All(Mark(FilterOperator.NotEqual, "m-001", false)));

        Assert.AreEqual(total, sensitive);
        Assert.IsLessThan(total, insensitive);
    }

    [TestMethod]
    public async Task CaseContainsIsExact()
    {
        var total = await CountAsync(FilterSet.All());

        Assert.AreEqual(0, await CountAsync(FilterSet.All(Mark(FilterOperator.Contains, "m-", true))));
        Assert.AreEqual(total, await CountAsync(FilterSet.All(Mark(FilterOperator.Contains, "M-", true))));
    }

    [TestMethod]
    public void WidenDropsCaseFlags()
    {
        var widened = CaseSensitiveRules.Widen(Trees()[0]);

        Assert.IsFalse(CaseSensitiveRules.Any(widened));
    }

    [TestMethod]
    public void WidenedTreeIsSuperset()
    {
        var snapshot = TestModel.Snapshot(Current);
        var levels = TestModel.SmallData.Levels;
        foreach (var tree in Trees().Concat(FakeFilterTrees.Generate(60)))
        {
            var widened = CaseSensitiveRules.Widen(tree);
            var exact = snapshot.Rows.Where(row => FakeFilterMatcher.Matches(snapshot, row, tree, levels))
                .Select(row => row.Id).ToHashSet();
            var native = snapshot.Rows.Where(row => FakeFilterMatcher.Matches(snapshot, row, widened, levels))
                .ToList();
            var refined = native.Where(row => FakeFilterMatcher.Matches(snapshot, row, tree, levels))
                .Select(row => row.Id).ToHashSet();

            Assert.IsTrue(exact.IsSubsetOf(native.Select(row => row.Id)), tree.ToString());
            Assert.IsTrue(exact.SetEquals(refined), tree.ToString());
        }
    }

    private static List<FilterSet> Trees()
    {
        return
        [
            FilterSet.All(Mark(FilterOperator.Equal, "M-001", true), Mark(FilterOperator.NotContains, "m-00", true)),
            FilterSet.Any(Mark(FilterOperator.BeginsWith, "m", true), Mark(FilterOperator.EndsWith, "1", true)),
            FilterSet.All(Mark(FilterOperator.NotEqual, "M-001", true)),
            FilterSet.Any(Mark(FilterOperator.NotEqual, "m-001", true),
                FilterSet.All(Mark(FilterOperator.NotContains, "M-0", true))),
            FilterSet.All(FilterSet.Any(Mark(FilterOperator.Greater, "M-5", true)),
                new FilterRule(FakeParameters.Comments, FilterOperator.NotEmpty))
        ];
    }

    private static FilterRule Mark(FilterOperator op, string value, bool caseSensitive)
    {
        return new FilterRule(FakeParameters.Mark, op, value, caseSensitive);
    }

    private static async Task<int> CountAsync(FilterSet root)
    {
        var model = new FakeModel(TestModel.SmallData);
        var ids = await model.EvaluateAsync(Current, new FilterDefinition([], root), null, CancellationToken.None);
        return ids.Count;
    }
}
