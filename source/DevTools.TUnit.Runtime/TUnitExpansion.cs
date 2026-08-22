using System.Reflection;
using TUnit.Core.Enums;
using TUnit.Core.Helpers;

namespace DevTools.TUnit.Runtime;

/// <summary>
/// 1-based class/method data indexes plus 0-based repeat, matching
/// TUnit.Engine <c>TestIdentifierService.GenerateTestId</c>.
/// Catalog display only reads <see cref="RepeatIndex"/>; the rest is UID.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
internal readonly record struct TUnitCombinationIndices(
    int ClassSourceIndex,
    int ClassLoopIndex,
    int MethodSourceIndex,
    int MethodLoopIndex,
    int RepeatIndex);

internal readonly record struct TUnitCombination(
    object?[] MethodArgs,
    TUnitCombinationIndices Indices,
    string? DisplayName,
    bool Deferred,
    (string Name, object? Value)[] Properties);

internal readonly record struct TUnitExpansionResult(
    TestMetadata? Metadata,
    IReadOnlyList<TUnitCombination> Combinations);

internal static class TUnitExpansion
{
    public static TUnitExpansionResult Expand(ITestEntrySource source, int index, string sessionId)
    {
        var filter = source.GetFilterData(index);
        try
        {
            var metadata = source.Materialize(index, sessionId).Single();
            return new TUnitExpansionResult(metadata, Expand(metadata, filter, sessionId));
        }
        catch (Exception)
        {
            return new TUnitExpansionResult(null, new[] { DeferredCombination(filter.MethodName) });
        }
    }

    private static IReadOnlyList<TUnitCombination> Expand(
        TestMetadata metadata,
        TestEntryFilterData filter,
        string sessionId)
    {
        var session = new ExpansionSession(metadata, sessionId);
        return BuildCombinations(
            Collect(metadata.ClassDataSources, session.ClassGenerator),
            Collect(metadata.DataSources, session.MethodGenerator),
            CollectProperties(metadata, session),
            RepeatTimes(metadata, filter));
    }

    private static List<TUnitCombination> BuildCombinations(
        IReadOnlyList<SourceRow> classRows,
        IReadOnlyList<SourceRow> methodRows,
        IReadOnlyList<(string Name, object? Value)[]> propertyRows,
        int repeats)
    {
        var combinations = new List<TUnitCombination>(
            classRows.Count * methodRows.Count * propertyRows.Count * repeats);

        foreach (var classRow in classRows)
        foreach (var methodRow in methodRows)
        foreach (var properties in propertyRows)
        {
            for (var repeat = 0; repeat < repeats; repeat++)
                combinations.Add(CreateCombination(classRow, methodRow, properties, repeat));
        }

        return combinations;
    }

    private static TUnitCombination CreateCombination(
        SourceRow classRow,
        SourceRow methodRow,
        (string Name, object? Value)[] properties,
        int repeat) =>
        new(
            methodRow.Args,
            new TUnitCombinationIndices(
                classRow.SourceIndex,
                classRow.LoopIndex,
                methodRow.SourceIndex,
                methodRow.LoopIndex,
                repeat),
            methodRow.DisplayName,
            Deferred: false,
            properties);

    private static TUnitCombination DeferredCombination(string methodName) =>
        new([], DeferredIndices, methodName, Deferred: true, []);

    private static readonly TUnitCombinationIndices DeferredIndices = new(1, 1, 1, 1, 0);

    private static List<(string Name, object? Value)[]> CollectProperties(
        TestMetadata metadata,
        ExpansionSession session)
    {
        var sources = ResolvePropertyDataSources(metadata);
        if (sources.Length == 0)
            return new List<(string Name, object? Value)[]> { Array.Empty<(string Name, object? Value)>() };

        var byName = new Dictionary<string, List<object?>>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (!byName.TryGetValue(source.PropertyName, out var values))
            {
                values = [];
                byName[source.PropertyName] = values;
            }

            foreach (var row in CollectSource(source.DataSource, session.PropertyGenerator, sourceIndex: 0))
                values.Add(row.Args.Length > 0 ? row.Args[0] : null);
        }

        return CartesianPropertySets(byName);
    }

    private static List<(string Name, object? Value)[]> CartesianPropertySets(
        Dictionary<string, List<object?>> byName)
    {
        var rows = new List<(string Name, object? Value)[]>
        {
            Array.Empty<(string Name, object? Value)>(),
        };
        foreach (var (name, values) in byName)
        {
            var propertyValues = values.Count == 0 ? new List<object?> { null } : values;
            var next = new List<(string Name, object? Value)[]>(rows.Count * propertyValues.Count);
            foreach (var row in rows)
            {
                foreach (var value in propertyValues)
                {
                    var extended = new (string Name, object? Value)[row.Length + 1];
                    row.CopyTo(extended, 0);
                    extended[^1] = (name, value);
                    next.Add(extended);
                }
            }

            rows = next;
        }

        return rows;
    }

    private static List<SourceRow> Collect(IDataSourceAttribute[] sources, DataGeneratorMetadata generator)
    {
        if (sources.Length == 0)
            return [new SourceRow([], 1, 1, null)];

        var rows = new List<SourceRow>();
        for (var sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            var engineSourceIndex = sourceIndex + 1;
            var collected = CollectSource(sources[sourceIndex], generator, engineSourceIndex);
            if (collected.Count == 0)
                rows.Add(new SourceRow([], engineSourceIndex, 1, DisplayNameOf(sources[sourceIndex])));
            else
                rows.AddRange(collected);
        }

        return rows;
    }

    private static List<SourceRow> CollectSource(
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

    private static object?[] Normalize(object?[] row)
    {
        if (row.Length != 1)
            return row;

        var unwrapped = DataSourceHelpers.UnwrapTupleAot(row[0]);
        return unwrapped.Length > 1 ? unwrapped : row;
    }

    private static string? DisplayNameOf(IDataSourceAttribute source) =>
        source is ArgumentsAttribute arguments && !string.IsNullOrWhiteSpace(arguments.DisplayName)
            ? arguments.DisplayName
            : null;

    private static int RepeatTimes(TestMetadata metadata, TestEntryFilterData filter)
    {
        var repeatCount = metadata.RepeatCount is > 0
            ? metadata.RepeatCount.Value
            : filter.RepeatCount;
        return repeatCount > 0 ? repeatCount + 1 : 1;
    }

    private static DataGeneratorMetadata CreateGenerator(
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

    private static ParameterMetadata[] FilterCancellation(ParameterMetadata[] parameters)
    {
        if (parameters.Length == 0)
            return parameters;
        var last = parameters[^1];
        return last.Type == typeof(CancellationToken)
            ? parameters.Take(parameters.Length - 1).ToArray()
            : parameters;
    }

    private static IMemberMetadata[] CastMembers(ParameterMetadata[] parameters) => [.. parameters];

    private static PropertyDataSource[] ResolvePropertyDataSources(TestMetadata metadata)
    {
        if (metadata.PropertyDataSources.Length > 0)
            return metadata.PropertyDataSources;

        // TUnit 1.65 TestEntryFactory omits injectableProperties; reflect writable properties instead.
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

    private sealed class ExpansionSession
    {
        public ExpansionSession(TestMetadata metadata, string sessionId)
        {
            var builder = new TestBuilderContext { TestMetadata = metadata.MethodMetadata };
            var accessor = new TestBuilderContextAccessor(builder);
            MethodGenerator = CreateGenerator(metadata, accessor, DataGeneratorType.TestParameters, sessionId);
            ClassGenerator = CreateGenerator(metadata, accessor, DataGeneratorType.ClassParameters, sessionId);
            PropertyGenerator = CreateGenerator(metadata, accessor, DataGeneratorType.Property, sessionId);
        }

        public DataGeneratorMetadata MethodGenerator { get; }
        public DataGeneratorMetadata ClassGenerator { get; }
        public DataGeneratorMetadata PropertyGenerator { get; }
    }

    private readonly record struct SourceRow(object?[] Args, int SourceIndex, int LoopIndex, string? DisplayName);
}
