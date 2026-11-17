using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Engine;

public sealed record SourceCatalog(SourceId Source, IReadOnlyList<ParameterInfo> Parameters, IReadOnlyCollection<long> Groups);

public sealed class ParameterScopes(Func<IReadOnlyList<IReadOnlyList<ParameterInfo>>, IReadOnlyList<ParameterInfo>> merge)
{
    private readonly Dictionary<long, string?> _categoryNames = [];
    private readonly Dictionary<string, IReadOnlyList<ParameterInfo>> _merged = new(StringComparer.Ordinal);
    private IReadOnlyList<ParameterInfo>? _all;
    private Func<long, string?> _categoryLabel = _ => null;
    private IReadOnlyList<SourceCatalog> _catalogs = [];

    public ParameterScopes()
        : this(ParameterCatalog.Merge)
    {
    }

    public int CatalogCount => _catalogs.Count;

    public int ScopeCount => _merged.Count;

    public IReadOnlyList<ParameterInfo> All => _all ??= Named(merge(_catalogs.Select(item => item.Parameters).ToList()));

    public void Set(
        IReadOnlyList<SourceCatalog> catalogs,
        Func<SourceId, string>? modelLabel = null,
        Func<long, string?>? categoryLabel = null)
    {
        _catalogs = modelLabel is null ? catalogs : catalogs.Select(catalog => Labeled(catalog, modelLabel(catalog.Source))).ToList();
        _categoryLabel = categoryLabel ?? (_ => null);
        _categoryNames.Clear();
        _all = null;
        _merged.Clear();
    }

    public IReadOnlyList<ParameterInfo> For(EffectiveScope scope)
    {
        if (scope.IsEmpty)
        {
            return [];
        }

        var key = KeyOf(scope);
        if (!_merged.TryGetValue(key, out var parameters))
        {
            parameters = Merge(scope);
            _merged[key] = parameters;
        }

        return parameters;
    }

    public ParameterInfo? Find(ParameterKey key, EffectiveScope scope)
    {
        return For(scope).FirstOrDefault(parameter => parameter.Key == key)
               ?? All.FirstOrDefault(parameter => parameter.Key == key);
    }

    private static SourceCatalog Labeled(SourceCatalog catalog, string label)
    {
        IReadOnlyList<string> models = [label];
        return catalog with { Parameters = catalog.Parameters.Select(parameter => parameter with { Models = models }).ToList() };
    }

    private IReadOnlyList<ParameterInfo> Merge(EffectiveScope scope)
    {
        var catalogs = _catalogs.Where(item => scope.Documents.Contains(item.Source)).ToList();
        if (scope.Categories is null)
        {
            return catalogs.Count == _catalogs.Count
                ? All
                : Named(merge(catalogs.Select(item => item.Parameters).ToList()));
        }

        var groups = scope.Categories.ToHashSet();
        return Named(merge(catalogs
            .Where(item => item.Groups.Any(groups.Contains))
            .Select(item => (IReadOnlyList<ParameterInfo>)item.Parameters
                .Where(parameter => parameter.Categories.Overlaps(groups))
                .ToList())
            .ToList()));
    }

    private IReadOnlyList<ParameterInfo> Named(IReadOnlyList<ParameterInfo> parameters)
    {
        return parameters
            .Select(parameter => parameter with { CategoryNames = NamesOf(parameter.Categories) })
            .OrderBy(parameter => parameter.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(parameter => parameter.CategorySummary, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<string> NamesOf(CategoryIdSet categories)
    {
        var names = new List<string>();
        foreach (var id in categories)
        {
            if (!_categoryNames.TryGetValue(id, out var name))
            {
                name = _categoryLabel(id);
                _categoryNames[id] = name;
            }

            if (name is not null)
            {
                names.Add(name);
            }
        }

        names.Sort(StringComparer.CurrentCultureIgnoreCase);
        return names;
    }

    private static string KeyOf(EffectiveScope scope)
    {
        var documents = string.Join("\n", scope.Documents.Select(source => source.Value).OrderBy(value => value,
            StringComparer.Ordinal));
        var categories = scope.Categories is null
            ? "*"
            : string.Join(",", scope.Categories.OrderBy(id => id));
        return documents + "|" + categories;
    }
}
