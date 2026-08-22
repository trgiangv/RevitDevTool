using System.Reflection;
using DevTools.Testing.Abstractions.Contracts;
using TUnit.Core;
using TUnit.Core.Enums;
using TUnit.Core.Helpers;

namespace DevTools.TUnit.Runtime;

internal readonly record struct TUnitCombination(
    object?[] ClassArgs,
    object?[] MethodArgs,
    int ClassSourceIndex,
    int ClassLoopIndex,
    int MethodSourceIndex,
    int MethodLoopIndex,
    int RepeatIndex,
    string? DisplayName,
    bool Deferred,
    string? ExpansionError,
    (string Name, object? Value)[] Properties,
    int PropertyLoopIndex = 0,
    TestMetadata? Metadata = null);

internal static class TUnitExpansion
{
    public static IReadOnlyList<TUnitCombination> Expand(
        ITestEntrySource source,
        int index,
        string sessionId,
        TestingDiscoveryOptions options)
    {
        var filter = source.GetFilterData(index);
        try
        {
            var metadata = source.Materialize(index, sessionId).Single();
            return Expand(metadata, filter);
        }
        catch (Exception ex)
        {
            return
            [
                new TUnitCombination(
                    [],
                    [],
                    1,
                    1,
                    1,
                    1,
                    0,
                    filter.MethodName,
                    Deferred: true,
                    ExpansionError: ex.Message,
                    Properties: []),
            ];
        }
    }

    public static IReadOnlyList<TUnitCombination> Expand(TestMetadata metadata, TestEntryFilterData filter)
    {
        var builder = new TestBuilderContext { TestMetadata = metadata.MethodMetadata };
        var accessor = new TestBuilderContextAccessor(builder);
        var methodGen = CreateGenerator(metadata, accessor, DataGeneratorType.TestParameters, "expansion");
        var classGen = CreateGenerator(metadata, accessor, DataGeneratorType.ClassParameters, "expansion");

        var classRows = Collect(metadata.ClassDataSources, classGen);
        var methodRows = Collect(metadata.DataSources, methodGen);
        var propertyRows = CollectProperties(metadata, "expansion");
        var repeats = RepeatTimes(metadata, filter);
        var combinations = new List<TUnitCombination>(
            classRows.Count * methodRows.Count * propertyRows.Count * repeats);

        foreach (var classRow in classRows)
        {
            foreach (var methodRow in methodRows)
            {
                for (var propertyIndex = 0; propertyIndex < propertyRows.Count; propertyIndex++)
                {
                    var properties = propertyRows[propertyIndex];
                    for (var repeat = 0; repeat < repeats; repeat++)
                    {
                        combinations.Add(new TUnitCombination(
                            classRow.Args,
                            methodRow.Args,
                            classRow.SourceIndex,
                            classRow.LoopIndex,
                            methodRow.SourceIndex,
                            methodRow.LoopIndex,
                            repeat,
                            methodRow.DisplayName,
                            Deferred: false,
                            ExpansionError: null,
                            Properties: properties,
                            PropertyLoopIndex: propertyIndex,
                            Metadata: metadata));
                    }
                }
            }
        }

        return combinations;
    }

    static List<(string Name, object? Value)[]> CollectProperties(TestMetadata metadata, string session)
    {
        var sources = ResolvePropertyDataSources(metadata);
        if (sources.Length == 0)
            return [[]];

        var builder = new TestBuilderContext { TestMetadata = metadata.MethodMetadata };
        var accessor = new TestBuilderContextAccessor(builder);
        var gen = CreateGenerator(metadata, accessor, DataGeneratorType.Property, session);
        var byName = new Dictionary<string, List<object?>>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (!byName.TryGetValue(source.PropertyName, out var values))
            {
                values = [];
                byName[source.PropertyName] = values;
            }

            foreach (var row in CollectSource(source.DataSource, gen, 0))
                values.Add(row.Args.Length > 0 ? row.Args[0] : null);
        }

        IEnumerable<(string Name, object? Value)[]> seed = [[]];
        foreach (var pair in byName)
        {
            var property = pair.Key;
            var values = pair.Value.Count == 0 ? new List<object?> { null } : pair.Value;
            seed = seed.SelectMany(prefix => values.Select(value =>
            {
                var next = new (string Name, object? Value)[prefix.Length + 1];
                prefix.CopyTo(next, 0);
                next[^1] = (property, value);
                return next;
            }));
        }

        return seed.ToList();
    }

    static List<SourceRow> Collect(
        IDataSourceAttribute[] sources,
        DataGeneratorMetadata generator)
    {
        if (sources.Length == 0)
        {
            return
            [
                new SourceRow([], 1, 1, null),
            ];
        }

        var rows = new List<SourceRow>();
        for (var sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            var engineSourceIndex = sourceIndex + 1;
            var collected = CollectSource(sources[sourceIndex], generator, engineSourceIndex);
            if (collected.Count == 0)
            {
                rows.Add(new SourceRow([], engineSourceIndex, 1, DisplayNameOf(sources[sourceIndex])));
                continue;
            }

            rows.AddRange(collected);
        }

        return rows.Count == 0 ? [new SourceRow([], 1, 1, null)] : rows;
    }

    static List<SourceRow> CollectSource(
        IDataSourceAttribute source,
        DataGeneratorMetadata generator,
        int sourceIndex)
    {
        var rows = new List<SourceRow>();
        var enumerator = source.GetDataRowsAsync(generator).GetAsyncEnumerator();
        try
        {
            var loop = 1;
            while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                var factory = enumerator.Current;
                var raw = factory().GetAwaiter().GetResult() ?? [];
                rows.Add(new SourceRow(Normalize(raw), sourceIndex, loop, DisplayNameOf(source)));
                loop++;
            }
        }
        finally
        {
            enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        return rows;
    }

    static object?[] Normalize(object?[] row)
    {
        if (row.Length != 1)
            return row;

        var unwrapped = DataSourceHelpers.UnwrapTupleAot(row[0]);
        return unwrapped.Length > 1 ? unwrapped : row;
    }

    static string? DisplayNameOf(IDataSourceAttribute source) =>
        source is ArgumentsAttribute arguments && !string.IsNullOrWhiteSpace(arguments.DisplayName)
            ? arguments.DisplayName
            : null;

    static int RepeatTimes(TestMetadata metadata, TestEntryFilterData filter)
    {
        var repeatCount = metadata.RepeatCount is > 0
            ? metadata.RepeatCount.Value
            : filter.RepeatCount;
        return repeatCount > 0 ? repeatCount + 1 : 1;
    }

    static DataGeneratorMetadata CreateGenerator(
        TestMetadata metadata,
        TestBuilderContextAccessor accessor,
        DataGeneratorType type,
        string sessionId)
    {
        var members = type switch
        {
            DataGeneratorType.ClassParameters => CastMembers(metadata.MethodMetadata.Class.Parameters),
            DataGeneratorType.TestParameters => CastMembers(FilterCancellation(metadata.MethodMetadata.Parameters)),
            DataGeneratorType.Property => [.. metadata.MethodMetadata.Class.Properties],
            _ => [],
        };

        return new DataGeneratorMetadata
        {
            TestBuilderContext = accessor,
            MembersToGenerate = members,
            TestInformation = metadata.MethodMetadata,
            Type = type,
            TestSessionId = sessionId,
            TestClassInstance = null,
            ClassInstanceArguments = null,
        };
    }

    static ParameterMetadata[] FilterCancellation(ParameterMetadata[] parameters)
    {
        if (parameters.Length == 0)
            return parameters;
        var last = parameters[^1];
        return last.Type == typeof(CancellationToken)
            ? parameters.Take(parameters.Length - 1).ToArray()
            : parameters;
    }

    static IMemberMetadata[] CastMembers(ParameterMetadata[] parameters) => [.. parameters];

    static PropertyDataSource[] ResolvePropertyDataSources(TestMetadata metadata)
    {
        if (metadata.PropertyDataSources.Length > 0)
            return metadata.PropertyDataSources;

        // TUnit 1.65 TestEntryFactory omits injectableProperties, so generated metadata has
        // empty PropertyDataSources. Reflect IDataSourceAttribute on writable properties.
        return metadata.TestClassType
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .SelectMany(property => property.GetCustomAttributes()
                .OfType<IDataSourceAttribute>()
                .Select(source => new PropertyDataSource
                {
                    PropertyName = property.Name,
                    PropertyType = property.PropertyType,
                    DataSource = source,
                }))
            .ToArray();
    }

    readonly record struct SourceRow(object?[] Args, int SourceIndex, int LoopIndex, string? DisplayName);
}
