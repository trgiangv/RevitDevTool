using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Tests.Shared;

public sealed record FakeModelOptions
{
    public int Seed { get; init; } = 42;

    public int CurrentElements { get; init; } = 100_000;

    public int LinkElements { get; init; } = 20_000;

    public DocumentCapabilities LinkCapabilities { get; init; } =
        DocumentCapabilities.WholeModel | DocumentCapabilities.ReferenceCommands;

    public string ModelId { get; init; } = FakeModelGenerator.ModelId;

    public int ExtraLinks { get; init; }

    public static FakeModelOptions Million { get; } = new()
    {
        CurrentElements = 200_000,
        LinkElements = 200_000,
        ExtraLinks = 2
    };
}

public sealed record FakeSource(SourceId Id, string FileName, IReadOnlyList<TypeRecord> Types, IReadOnlyList<ElementRecord> Elements);

public sealed record FakeModelData(
    DocumentKey CurrentModel,
    IReadOnlyList<DocumentInfo> Documents,
    IReadOnlyDictionary<SourceId, FakeSource> Sources,
    IReadOnlyDictionary<long, string> Levels)
{
    public DocumentInfo Document(DocumentKey key)
    {
        return Documents.First(document => document.Key == key);
    }

    public FakeSource SourceOf(DocumentKey key)
    {
        return Sources[Document(key).Source];
    }
}

public static class FakeModelGenerator
{
    public const string ModelId = "fake-model";
    public const long ArchFirstInstance = 9001;
    public const long ArchSecondInstance = 9002;
    public const long MepInstance = 9003;

    public static readonly SourceId CurrentSource = new("current");
    public static readonly SourceId ArchSource = new("arch");
    public static readonly SourceId MepSource = new("mep");

    public static readonly DocumentKey Current = DocumentKey.Current(ModelId);
    public static readonly DocumentKey ArchFirst = DocumentKey.Link(ModelId, ArchFirstInstance);
    public static readonly DocumentKey ArchSecond = DocumentKey.Link(ModelId, ArchSecondInstance);
    public static readonly DocumentKey Mep = DocumentKey.Link(ModelId, MepInstance);

    public static readonly TreeSchema Schema = new(["Category", "Family", "Type", "Instance"]);

    internal static readonly IReadOnlyList<CategorySpec> Categories =
    [
        new(-2000011, "Walls", 'A', 14),
        new(-2000023, "Doors", 'A', 6),
        new(-2000014, "Windows", 'A', 6),
        new(-2000032, "Floors", 'A', 3),
        new(-2000035, "Roofs", 'A', 1),
        new(-2000038, "Ceilings", 'A', 3),
        new(-2000080, "Furniture", 'A', 7),
        new(-2000151, "Generic Models", 'A', 3),
        new(-2000100, "Structural Columns", 'S', 4),
        new(-2001320, "Structural Framing", 'S', 6),
        new(-2008000, "Ducts", 'M', 10),
        new(-2008010, "Duct Fittings", 'M', 8),
        new(-2008013, "Air Terminals", 'M', 4),
        new(-2001140, "Mechanical Equipment", 'M', 2),
        new(-2008044, "Pipes", 'M', 10),
        new(-2008049, "Pipe Fittings", 'M', 9),
        new(-2001160, "Plumbing Fixtures", 'M', 2),
        new(-2008099, "Sprinklers", 'M', 3),
        new(-2008130, "Cable Trays", 'M', 2),
        new(-2008132, "Conduits", 'M', 3),
        new(-2001120, "Lighting Fixtures", 'M', 4)
    ];

    private static readonly string[] LevelNames = ["Level 0", "Level 1", "Level 2", "Level 3", "Roof"];

    public static FakeModelData Generate(FakeModelOptions? options = null)
    {
        options ??= new FakeModelOptions();
        var levels = LevelNames.Select((name, index) => (Id: 300L + index, Name: name))
            .ToDictionary(level => level.Id, level => level.Name);

        var modelId = options.ModelId;
        var currentSource = modelId == ModelId ? CurrentSource : new SourceId($"current-{modelId}");
        var currentKey = DocumentKey.Current(modelId);
        var current = CreateSource(currentSource, "Current model", "AMS", options.CurrentElements, options.Seed,
            1_000_000, levels);
        var arch = CreateSource(ArchSource, "Arch.rvt", "AS", options.LinkElements, options.Seed + 1, 2_000_000,
            levels);
        var mep = CreateSource(MepSource, "MEP-Mech.rvt", "M", options.LinkElements, options.Seed + 2, 3_000_000,
            levels);

        const DocumentCapabilities currentCapabilities =
            DocumentCapabilities.HideById | DocumentCapabilities.HideGroupEverywhere;
        var documents = new List<DocumentInfo>
        {
            new(currentKey, currentSource, "Current model", true, currentCapabilities),
            new(DocumentKey.Link(modelId, ArchFirstInstance), ArchSource, "Arch.rvt", true, options.LinkCapabilities),
            new(DocumentKey.Link(modelId, ArchSecondInstance), ArchSource, "Arch.rvt (2)", true,
                options.LinkCapabilities),
            new(DocumentKey.Link(modelId, MepInstance), MepSource, "MEP-Mech.rvt", true, options.LinkCapabilities)
        };

        var sources = new Dictionary<SourceId, FakeSource>
        {
            [currentSource] = current,
            [ArchSource] = arch,
            [MepSource] = mep
        };

        for (var index = 1; index <= options.ExtraLinks; index++)
        {
            var id = new SourceId($"link-{index}");
            var fileName = $"Link-{index}.rvt";
            sources[id] = CreateSource(id, fileName, "ASM", options.LinkElements, options.Seed + 2 + index,
                3_000_000 + index * 1_000_000L, levels);
            documents.Add(new DocumentInfo(DocumentKey.Link(modelId, MepInstance + index), id, fileName, true,
                options.LinkCapabilities));
        }

        return new FakeModelData(currentKey, documents, sources, levels);
    }

    public static DocumentSnapshot BuildSnapshot(FakeSource source, int groupingDepth = TreeSchema.MaxGroupingDepth)
    {
        return BuildSnapshot(source, null, false, groupingDepth);
    }

    public static DocumentSnapshot BuildSnapshot(
        FakeSource source,
        DocumentVersion? stamp,
        bool reversed,
        int groupingDepth = TreeSchema.MaxGroupingDepth)
    {
        var builder = new DocumentSnapshotBuilder(source.Id, groupingDepth);
        if (stamp is { } version)
        {
            builder.WithStamp(version);
        }

        foreach (var type in reversed ? source.Types.Reverse() : source.Types)
        {
            builder.AddType(type);
        }

        foreach (var element in reversed ? source.Elements.Reverse() : source.Elements)
        {
            builder.AddElement(element);
        }

        return builder.Build();
    }

    private static FakeSource CreateSource(
        SourceId id,
        string fileName,
        string disciplines,
        int elementCount,
        int seed,
        long idBase,
        IReadOnlyDictionary<long, string> levels)
    {
        var random = new SeededRandom(seed);
        var categories = Categories.Where(category => disciplines.IndexOf(category.Discipline) >= 0).ToList();
        var types = new List<TypeRecord>();
        var typesByCategory = new List<List<TypeRecord>>();
        var nextTypeId = idBase;
        foreach (var category in categories)
        {
            var list = new List<TypeRecord>();
            var familyCount = 1 + random.Next(8);
            for (var family = 1; family <= familyCount; family++)
            {
                var familyName = $"{category.Name} Family {family}";
                var typeCount = 1 + random.Next(6);
                for (var type = 1; type <= typeCount; type++)
                {
                    var record = new TypeRecord(nextTypeId++, category.Id, category.Name, familyName,
                        $"{familyName} Type {type}");
                    list.Add(record);
                    types.Add(record);
                }
            }

            typesByCategory.Add(list);
        }

        var totalWeight = categories.Sum(category => category.Weight);
        var levelIds = levels.Keys.ToArray();
        var elements = new List<ElementRecord>(elementCount);
        var nextId = idBase + 100_000;
        for (var index = 0; index < elementCount; index++)
        {
            var pick = random.Next(totalWeight);
            var categoryIndex = 0;
            while (pick >= categories[categoryIndex].Weight)
            {
                pick -= categories[categoryIndex].Weight;
                categoryIndex++;
            }

            var candidates = typesByCategory[categoryIndex];
            var skew = random.NextDouble();
            var typeIndex = (int)(skew * skew * candidates.Count);
            nextId += 1 + random.Next(3);
            elements.Add(new ElementRecord(nextId, candidates[typeIndex].TypeId, levelIds[random.Next(levelIds.Length)],
                1 + random.Next(4)));
        }

        return new FakeSource(id, fileName, types, elements);
    }

    internal sealed record CategorySpec(long Id, string Name, char Discipline, int Weight);

    private sealed class SeededRandom(int seed)
    {
        private ulong _state = (ulong)seed * 0x9E3779B97F4A7C15UL + 1;

        public int Next(int maxExclusive)
        {
            return (int)(NextUInt64() % (ulong)maxExclusive);
        }

        public double NextDouble()
        {
            return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
        }

        private ulong NextUInt64()
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            return _state;
        }
    }
}
