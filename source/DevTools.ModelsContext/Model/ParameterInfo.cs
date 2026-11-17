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
    ParameterBinding Bindings = ParameterBinding.None)
{
    public const int SummaryNames = 2;

    public CategoryIdSet Categories { get; init; } = CategoryIdSet.Empty;

    public string? Discipline { get; init; }

    public string? Group { get; init; }

    public IReadOnlyList<string> Families { get; init; } = [];

    public IReadOnlyList<string> Models { get; init; } = [];

    public IReadOnlyList<string> CategoryNames { get; init; } = [];

    public bool IsMeasured => Discipline is not null;

    public ParameterBinding Section => (Bindings & ParameterBinding.Instance) != 0 || Bindings == ParameterBinding.None
        ? ParameterBinding.Instance
        : ParameterBinding.Type;

    public bool IsType => Section == ParameterBinding.Type;

    public ParameterOrigin Source
    {
        get
        {
            if ((Origins & ParameterOrigin.Shared) != 0)
            {
                return ParameterOrigin.Shared;
            }

            if ((Origins & ParameterOrigin.Project) != 0)
            {
                return ParameterOrigin.Project;
            }

            return (Origins & ParameterOrigin.Family) != 0 ? ParameterOrigin.Family : ParameterOrigin.BuiltIn;
        }
    }

    public string CategorySummary => Summary(CategoryNames);

    public string SpokenName
    {
        get
        {
            var parts = new List<string>(3) { DisplayName };
            if (Source != ParameterOrigin.BuiltIn)
            {
                parts.Add(EnumText.Of(Source));
            }

            if (IsType)
            {
                parts.Add(EnumText.Of(ParameterBinding.Type));
            }

            return string.Join(", ", parts);
        }
    }

    public static string Summary(IReadOnlyList<string> names)
    {
        if (names.Count <= SummaryNames)
        {
            return string.Join(", ", names);
        }

        return string.Join(", ", names.Take(SummaryNames)) + " +" + (names.Count - SummaryNames).ToString(System.Globalization.CultureInfo.CurrentCulture);
    }

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
                    : parameter;
            }
        }

        return merged.Values
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
            Discipline = known.Discipline ?? other.Discipline,
            Group = known.Group ?? other.Group,
            Origins = known.Origins | other.Origins,
            Bindings = known.Bindings | other.Bindings,
            Categories = known.Categories.Union(other.Categories),
            Families = Union(known.Families, other.Families),
            Models = Union(known.Models, other.Models)
        };
    }

    private static IReadOnlyList<string> Union(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (right.Count == 0)
        {
            return left;
        }

        return left.Count == 0 ? right : left.Concat(right).Distinct(StringComparer.Ordinal).ToList();
    }
}
