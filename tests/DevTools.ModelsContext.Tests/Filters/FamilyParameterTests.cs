using Bogus;
using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Tests.Shared;
using System.Globalization;

namespace DevTools.ModelsContext.Tests.Filters;

[TestClass]
public sealed class FamilyParameterTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;

    [TestMethod]
    public void KeyMapsToTwoIds()
    {
        var snapshot = TestModel.Snapshot(Current);

        Assert.HasCount(2, FakeParameters.IdsOf(snapshot, FakeParameters.SwingAngle));
        Assert.IsTrue(Doors(snapshot).Any(row => !Has(snapshot, row)));
    }

    [TestMethod]
    public async Task HasValueSpansFamilies()
    {
        var snapshot = TestModel.Snapshot(Current);
        var expected = Doors(snapshot).Count(row => Value(snapshot, row) is { Length: > 0 });

        Assert.AreEqual(expected, await CountAsync(FilterOperator.NotEmpty));
    }

    [TestMethod]
    public async Task MissingCountsOtherFamilies()
    {
        var snapshot = TestModel.Snapshot(Current);
        var expected = Doors(snapshot).Count(row => !Has(snapshot, row));

        Assert.AreEqual(expected, await CountAsync(FilterOperator.Undefined));
    }

    [TestMethod]
    public async Task EqualsAndNotEqualsSplitValues()
    {
        var snapshot = TestModel.Snapshot(Current);
        var faker = new Faker { Random = new Randomizer(FakeCatalogs.Seed) };
        foreach (var angle in Enumerable.Range(0, 5).Select(_ => faker.Random.Int(1, 89)))
        {
            var text = angle.ToString(CultureInfo.InvariantCulture);
            var equal = Doors(snapshot).Count(row => Value(snapshot, row) == text);
            var notEqual = Doors(snapshot).Count(row => Value(snapshot, row) is { Length: > 0 } value && value != text);

            Assert.AreEqual(equal, await CountAsync(FilterOperator.Equal, text), text);
            Assert.AreEqual(notEqual, await CountAsync(FilterOperator.NotEqual, text), text);
        }
    }

    private static IEnumerable<ElementRow> Doors(DocumentSnapshot snapshot)
    {
        return snapshot.Rows.Where(row => snapshot.Groups[row.GroupIndex].GroupId == TestModel.Doors);
    }

    private static bool Has(DocumentSnapshot snapshot, ElementRow row)
    {
        return FakeParameters.IdOf(snapshot, row, FakeParameters.SwingAngle) is not null;
    }

    private static string? Value(DocumentSnapshot snapshot, ElementRow row)
    {
        return FakeParameters.TryGet(snapshot, row, FakeParameters.SwingAngle, TestModel.SmallData.Levels, out var value)
            ? value
            : null;
    }

    private static async Task<int> CountAsync(FilterOperator op, string value = "")
    {
        var model = new FakeModel(TestModel.SmallData);
        var ids = await model.EvaluateAsync(Current,
            new FilterDefinition([TestModel.Doors], FilterSet.All(new FilterRule(FakeParameters.SwingAngle, op, value))),
            null, CancellationToken.None);
        return ids.Count;
    }
}
