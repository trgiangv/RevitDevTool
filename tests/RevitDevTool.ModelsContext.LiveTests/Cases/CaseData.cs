using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RevitDevTool.ModelsContext.LiveTests;

internal class CaseItem
{
    public string Id { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public JsonElement Expected { get; set; }

    public List<string> ManualSteps { get; set; } = [];

    public List<string> Images { get; set; } = [];

    [JsonIgnore]
    public string ExpectedText => Expected.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null => string.Empty,
        JsonValueKind.String => Expected.GetString() ?? string.Empty,
        _ => Expected.GetRawText()
    };

    [JsonIgnore]
    public int? ExpectedCount => Expected.ValueKind == JsonValueKind.Number ? Expected.GetInt32() : null;

    public override string ToString()
    {
        return Id;
    }
}

internal sealed class FilterCaseData : CaseItem
{
    public string Docs { get; set; } = "Both";

    public string Scope { get; set; } = "WholeModel";

    public string? HostLengthUnit { get; set; }

    public NodeData Filter { get; set; } = new();
}

internal sealed class NodeData
{
    public List<string>? Categories { get; set; }

    public string? Match { get; set; }

    public ScopeData? Scope { get; set; }

    public List<NodeData>? Rules { get; set; }

    public string? Parameter { get; set; }

    public string? Operator { get; set; }

    public string Value { get; set; } = string.Empty;

    public bool CaseSensitive { get; set; }

    public bool Picked { get; set; }

    [JsonIgnore]
    public bool IsGroup => Parameter is null;
}

internal sealed class ScopeData
{
    public List<string> Documents { get; set; } = [];

    public List<string> Categories { get; set; } = [];
}

internal sealed class ParameterData
{
    public string? BuiltIn { get; set; }

    public string? Name { get; set; }

    public string? Spec { get; set; }

    public string? SharedFirstIn { get; set; }
}

internal sealed class SampleFacts
{
    public string HostTitle { get; set; } = string.Empty;

    public string LinkTitle { get; set; } = string.Empty;

    public string LinkLabel { get; set; } = string.Empty;

    public string SiteLinkTitle { get; set; } = string.Empty;

    public string ViewName { get; set; } = string.Empty;

    public string ThreeDViewName { get; set; } = string.Empty;

    public string MissingSource { get; set; } = string.Empty;

    public Dictionary<string, string> CategoryNames { get; set; } = [];

    public Dictionary<string, ParameterData> Parameters { get; set; } = [];
}

internal sealed class UiFlowCase : CaseItem
{
    public string Test { get; set; } = string.Empty;

    public List<string> Cases { get; set; } = [];

    public string? Category { get; set; }
}

internal class CaseFile<T>
{
    public List<T> Cases { get; set; } = [];
}

internal sealed class CaseFile<TSetup, T> : CaseFile<T> where TSetup : new()
{
    public TSetup Setup { get; set; } = new();
}

internal static class CaseFiles
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    private static readonly Lazy<SampleFacts> LazyFacts = new(() => Read<SampleFacts>("sample-facts.json"));

    private static readonly Lazy<List<FilterCaseData>> LazyFilters = new(() =>
        [.. Read<CaseFile<FilterCaseData>>("scopes.json").Cases, .. Read<CaseFile<FilterCaseData>>("filters.json").Cases]);

    private static readonly Lazy<List<UiFlowCase>> LazyFlows = new(() => Read<CaseFile<UiFlowCase>>("ui-flows.json").Cases);

    public static SampleFacts Facts => LazyFacts.Value;

    public static IReadOnlyList<FilterCaseData> Filters => LazyFilters.Value;

    public static IReadOnlyList<UiFlowCase> Flows => LazyFlows.Value;

    public static string Folder => FindFolder();

    public static FilterCaseData Filter(string id)
    {
        return Filters.FirstOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException($"No filter case '{id}' in scopes.json or filters.json.");
    }

    public static UiFlowCase Flow(string id)
    {
        return Flows.FirstOrDefault(item => item.Id == id) ?? throw new KeyNotFoundException($"No flow '{id}' in ui-flows.json.");
    }

    public static T Read<T>(string name)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(Folder, name)), Options)
               ?? throw new InvalidDataException(name + " is empty.");
    }

    private static string FindFolder([CallerFilePath] string source = "")
    {
        var beside = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(source)!)!, "Data");
        return Directory.Exists(beside) ? beside : Path.Combine(AppContext.BaseDirectory, "Data");
    }
}
