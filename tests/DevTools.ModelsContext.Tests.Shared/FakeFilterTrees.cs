using Bogus;
using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Tests.Shared;

public static class FakeFilterTrees
{
    public const int Seed = 4242;

    private static readonly FilterOperator[] TextOperators =
    [
        FilterOperator.Equal, FilterOperator.NotEqual, FilterOperator.Greater, FilterOperator.Less,
        FilterOperator.Contains, FilterOperator.NotContains, FilterOperator.BeginsWith, FilterOperator.NotBeginsWith,
        FilterOperator.EndsWith, FilterOperator.NotEndsWith, FilterOperator.NotEmpty
    ];

    public static IReadOnlyList<FilterSet> Generate(int count)
    {
        var faker = new Faker { Random = new Randomizer(Seed) };
        return Enumerable.Range(0, count).Select(_ => Set(faker, 0)).ToList();
    }

    public static string MixedCaseMark(Faker faker)
    {
        var text = $"M-{faker.Random.Int(0, 30):D3}".Substring(0, faker.Random.Int(1, 5));
        return faker.Random.Bool() ? text.ToLowerInvariant() : text;
    }

    private static FilterSet Set(Faker faker, int depth)
    {
        var children = Enumerable.Range(0, faker.Random.Int(1, 3))
            .Select(_ => depth < 2 && faker.Random.Bool(0.3f) ? (FilterNode)Set(faker, depth + 1) : Rule(faker))
            .ToList();
        return new FilterSet(faker.PickRandom(SetMatch.All, SetMatch.Any), children);
    }

    private static FilterRule Rule(Faker faker)
    {
        var parameter = faker.Random.Bool(0.8f) ? FakeParameters.Mark : FakeParameters.Comments;
        var value = parameter == FakeParameters.Mark ? MixedCaseMark(faker) : faker.PickRandom("check", "Check", "CH");
        return new FilterRule(parameter, faker.PickRandom(TextOperators), value, faker.Random.Bool(0.7f));
    }
}
