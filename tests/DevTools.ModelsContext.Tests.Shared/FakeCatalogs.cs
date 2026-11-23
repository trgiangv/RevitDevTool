using Bogus;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;

namespace DevTools.ModelsContext.Tests.Shared;

public static class FakeCatalogs
{
    public const int Seed = 5150;

    private static readonly string[] Specs = ["spec:length", "spec:text", "spec:int", "spec:yesno", "spec:area"];
    private static readonly string[] BuiltIns = ["ALL_MODEL_MARK", "ALL_MODEL_INSTANCE_COMMENTS", "LEVEL_PARAM",
        "FUNCTION_PARAM", "PHASE_CREATED", "WALL_BASE_OFFSET"];
    private static readonly long[] CategoryIds = [-2000011, -2000023, -2000014, -2008000, -2008044];

    public static IReadOnlyList<IReadOnlyList<ParameterInfo>> Documents(int count, int perDocument = 30)
    {
        var faker = new Faker { Random = new Randomizer(Seed) };
        var names = Enumerable.Range(0, perDocument).Select(_ => faker.Commerce.ProductAdjective()).Distinct().ToList();
        var catalogs = new List<IReadOnlyList<ParameterInfo>>();
        for (var index = 0; index < count; index++)
        {
            var catalog = new List<ParameterInfo>();
            for (var item = 0; item < perDocument; item++)
            {
                catalog.Add(Next(faker, names));
            }

            catalogs.Add(catalog.GroupBy(info => info.Key).Select(group => group.First()).ToList());
        }

        return catalogs;
    }

    private static ParameterInfo Next(Faker faker, List<string> names)
    {
        var origin = faker.PickRandom(ParameterOrigin.BuiltIn, ParameterOrigin.Shared, ParameterOrigin.Project,
            ParameterOrigin.Family);
        var name = faker.PickRandom<string>(names);
        var spec = faker.PickRandom(Specs);
        var key = origin switch
        {
            ParameterOrigin.BuiltIn => ParameterKey.BuiltIn(name, faker.PickRandom(BuiltIns)),
            ParameterOrigin.Shared => ParameterKey.Shared(name, new Guid(faker.Random.Int(1, 4), 0, 0, new byte[8])),
            _ => ParameterKey.Named(name, spec)
        };
        var storage = spec switch
        {
            "spec:length" or "spec:area" => ParameterStorage.Double,
            "spec:text" => ParameterStorage.String,
            _ => ParameterStorage.Integer
        };
        return new ParameterInfo(key, name,
            ValueKinds.Infer(storage, spec == "spec:yesno", storage == ParameterStorage.Double, false), storage, spec,
            null, origin, faker.PickRandom(ParameterBinding.Instance, ParameterBinding.Type))
        {
            Categories = CategoryIdSet.Of(faker.PickRandom(CategoryIds, faker.Random.Int(1, 3)))
        };
    }
}
