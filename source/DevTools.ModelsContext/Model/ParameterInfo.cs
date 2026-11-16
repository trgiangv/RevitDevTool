using System.ComponentModel;

namespace DevTools.ModelsContext.Model;

[Flags]
public enum ParameterOrigin
{
    None = 0,
    [Description("Built-in")]
    BuiltIn = 1,
    [Description("Shared")]
    Shared = 2,
    [Description("Project")]
    Project = 4,
    [Description("Family")]
    Family = 8
}

[Flags]
public enum ParameterBinding
{
    None = 0,
    [Description("Instance")]
    Instance = 1,
    [Description("Type")]
    Type = 2
}

public sealed record ParameterInfo(
    ParameterKey Key,
    string DisplayName,
    ValueKind Kind,
    ParameterStorage Storage = ParameterStorage.None,
    string? Spec = null,
    string? SpecLabel = null,
    ParameterOrigin Origins = ParameterOrigin.None,
    ParameterBinding Bindings = ParameterBinding.None,
    int Coverage = 1,
    int DocumentCount = 1)
{
    public CategoryIdSet Categories { get; init; } = CategoryIdSet.Empty;

    public bool IsInAllDocuments => Coverage >= DocumentCount;

    public override string ToString()
    {
        return DisplayName;
    }
}

public static class ParameterCatalog
{
    public static IReadOnlyList<ParameterInfo> Merge(IReadOnlyList<IReadOnlyList<ParameterInfo>> catalogs)
    {
        var merged = new Dictionary<ParameterKey, ParameterInfo>();
        foreach (var catalog in catalogs)
        {
            var seen = new HashSet<ParameterKey>();
            foreach (var parameter in catalog)
            {
                if (!seen.Add(parameter.Key))
                {
                    continue;
                }

                merged[parameter.Key] = merged.TryGetValue(parameter.Key, out var known)
                    ? Combine(known, parameter)
                    : parameter with { Coverage = 1 };
            }
        }

        return merged.Values
            .Select(parameter => parameter with { DocumentCount = catalogs.Count })
            .OrderBy(parameter => parameter.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(parameter => parameter.SpecLabel, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static ParameterInfo Combine(ParameterInfo known, ParameterInfo other)
    {
        return known with
        {
            Kind = known.Kind == ValueKind.Integer && other.Kind == ValueKind.Enumerated ? other.Kind : known.Kind,
            Storage = known.Storage == ParameterStorage.None ? other.Storage : known.Storage,
            Spec = known.Spec ?? other.Spec,
            SpecLabel = known.SpecLabel ?? other.SpecLabel,
            Origins = known.Origins | other.Origins,
            Bindings = known.Bindings | other.Bindings,
            Categories = known.Categories.Union(other.Categories),
            Coverage = known.Coverage + 1
        };
    }
}
