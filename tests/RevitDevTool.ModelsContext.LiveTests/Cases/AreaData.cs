namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed class ExplorerCase : CaseItem
{
    public string Check { get; set; } = string.Empty;

    public string Docs { get; set; } = string.Empty;

    public string? Category { get; set; }
}

internal sealed class CatalogCase : CaseItem
{
    public string Check { get; set; } = string.Empty;

    public string Docs { get; set; } = string.Empty;

    public string Parameter { get; set; } = string.Empty;

    public int? Count { get; set; }

    public string? Kind { get; set; }

    public List<string>? Origins { get; set; }

    public string? Bindings { get; set; }

    public List<string>? BindingsInclude { get; set; }

    public int Models { get; set; }

    public string Category { get; set; } = string.Empty;

    public int Total { get; set; }

    public int Values { get; set; }
}

internal sealed class HostFilterSetup
{
    public string Category { get; set; } = string.Empty;

    public RuleValue Text { get; set; } = new();

    public RuleValue Mark { get; set; } = new();

    public ReferenceValue Reference { get; set; } = new();

    public int SelectionCount { get; set; }

    public List<RuleValue> InvertedReadBack { get; set; } = [];
}

internal sealed class RuleValue
{
    public string Parameter { get; set; } = string.Empty;

    public string Operator { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

internal sealed class ReferenceValue
{
    public string Parameter { get; set; } = string.Empty;

    public string TargetClass { get; set; } = string.Empty;
}

internal sealed class EditSetup
{
    public string Category { get; set; } = string.Empty;

    public string Parameter { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public int Edited { get; set; }

    public double CopyOffsetFeet { get; set; }
}

internal sealed class VisibilityCase : CaseItem
{
    public string Tier { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public RgbData? Color { get; set; }

    public TargetData Target { get; set; } = new();

    public TargetData? Other { get; set; }

    public TargetData? Linked { get; set; }

    public VisibilityExpect Expect => System.Text.Json.JsonSerializer.Deserialize<VisibilityExpect>(Expected.GetRawText(), CaseFiles.Options)!;
}

internal sealed class RgbData
{
    public byte R { get; set; }

    public byte G { get; set; }

    public byte B { get; set; }

    public override string ToString()
    {
        return $"#{R:X2}{G:X2}{B:X2}";
    }
}

internal sealed class TargetData
{
    public string? Docs { get; set; }

    public string? Category { get; set; }

    public string? FilterCase { get; set; }

    public bool Link { get; set; }

    public bool Everywhere { get; set; }

    public string? LinkType { get; set; }

    public int? Repeat { get; set; }
}

internal sealed class VisibilityExpect
{
    public bool? TemporaryHideIsolate { get; set; }

    public bool? TemporaryProperties { get; set; }

    public bool? TargetVisible { get; set; }

    public bool? OtherVisible { get; set; }

    public bool? LinkVisible { get; set; }

    public bool? LinkedVisible { get; set; }

    public bool? TargetColor { get; set; }

    public bool? OtherColor { get; set; }

    public bool? LinkFilter { get; set; }

    public bool OnlyTargetStays { get; set; }

    public List<string>? Leftovers { get; set; }

    public string? Error { get; set; }

    public List<string>? PlannedTiers { get; set; }
}

internal sealed class ProbeData
{
    public string Docs { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Parameter { get; set; } = string.Empty;
}

internal static class Areas
{
    private static readonly Lazy<List<ExplorerCase>> LazyExplorer = new(() => CaseFiles.Read<CaseFile<ExplorerCase>>("explorer.json").Cases);
    private static readonly Lazy<List<CatalogCase>> LazyCatalog = new(() => CaseFiles.Read<CaseFile<CatalogCase>>("catalog.json").Cases);
    private static readonly Lazy<CaseFile<HostFilterSetup, CaseItem>> LazyHostFilters = new(() => CaseFiles.Read<CaseFile<HostFilterSetup, CaseItem>>("host-filters.json"));
    private static readonly Lazy<CaseFile<EditSetup, CaseItem>> LazyEdits = new(() => CaseFiles.Read<CaseFile<EditSetup, CaseItem>>("edits.json"));
    private static readonly Lazy<List<VisibilityCase>> LazyVisibility = new(() => CaseFiles.Read<CaseFile<VisibilityCase>>("visibility.json").Cases);
    private static readonly Lazy<List<ProbeData>> LazyProbes = new(() => CaseFiles.Read<List<ProbeData>>("probes.json"));

    public static IReadOnlyList<ExplorerCase> Explorer => LazyExplorer.Value;

    public static IReadOnlyList<CatalogCase> Catalog => LazyCatalog.Value;

    public static CaseFile<HostFilterSetup, CaseItem> HostFilters => LazyHostFilters.Value;

    public static CaseFile<EditSetup, CaseItem> Edits => LazyEdits.Value;

    public static IReadOnlyList<VisibilityCase> Visibility => LazyVisibility.Value;

    public static List<ProbeData> Probes => LazyProbes.Value;

    public static T Find<T>(IEnumerable<T> cases, string id) where T : CaseItem
    {
        return cases.FirstOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException($"No case '{id}' in the data files.");
    }

    public static IEnumerable<TestCaseData> Rows<T>(IEnumerable<T> cases) where T : CaseItem
    {
        return cases.Select(item => new TestCaseData(item.Id).SetArgDisplayNames(item.Id));
    }
}
