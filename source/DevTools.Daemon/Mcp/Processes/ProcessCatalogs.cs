using DevTools.Daemon.Mcp.Search;

namespace DevTools.Daemon.Mcp.Processes;

/// <summary>In-memory capability index for connected process sessions.</summary>
public sealed class ProcessCatalogs
{
    private static readonly CatalogType[] AllKinds = Enum.GetValues<CatalogType>();
    private readonly object _lock = new();
    private readonly Dictionary<int, ProcessCatalog> _catalogs = new();
    private readonly SearchIndex _index = new();

    public void Replace(ProcessCatalog catalog)
    {
        lock (_lock)
        {
            _catalogs[catalog.ProcessId] = catalog;
            RebuildIndex();
        }
    }

    public bool Remove(int processId)
    {
        lock (_lock)
        {
            if (!_catalogs.Remove(processId))
                return false;
            RebuildIndex();
            return true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _catalogs.Clear();
            _index.Rebuild([]);
        }
    }

    public IReadOnlyList<ProcessCatalog> List()
    {
        lock (_lock)
            return _catalogs.Values.OrderBy(catalog => catalog.ProcessId).ToArray();
    }

    public IReadOnlyList<Match> Search(
        string? query,
        IReadOnlyCollection<CatalogType>? kinds = null,
        int? processId = null,
        int limit = 50)
    {
        limit = Math.Clamp(limit <= 0 ? 50 : limit, 1, 500);
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return EnumerateItems(processId, kinds)
                    .OrderBy(item => item.Kind)
                    .ThenBy(item => item.Target, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.ProcessId)
                    .Take(limit)
                    .Select(item => new Match(item, item.ProcessId, 0))
                    .ToArray();
            }

            return _index.Search(query.Trim(), processId, kinds, limit);
        }
    }

    public IReadOnlyList<string> AvailableNames(int? processId = null)
    {
        lock (_lock)
            return EnumerateItems(processId, AllKinds)
                .Select(item => item.Target)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    public CatalogItem? Find(CatalogType kind, string target, int processId)
    {
        if (string.IsNullOrWhiteSpace(target))
            return null;

        CatalogItem[] candidates;
        lock (_lock)
            candidates = EnumerateItems(processId, [kind])
                .Where(item => string.Equals(item.Target, target, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();

        return candidates.Length == 1 ? candidates[0] : null;
    }

    private void RebuildIndex() => _index.Rebuild(EnumerateItems(null, AllKinds));

    private IEnumerable<CatalogItem> EnumerateItems(int? processId, IReadOnlyCollection<CatalogType>? kinds)
    {
        var kindFilter = kinds is { Count: > 0 } ? kinds : AllKinds;
        foreach (var catalog in _catalogs.Values.OrderBy(catalog => catalog.ProcessId))
        {
            if (processId is not null && catalog.ProcessId != processId)
                continue;

            foreach (var kind in kindFilter)
            foreach (var item in ItemsOfKind(catalog, kind))
                yield return item;
        }
    }

    private static IEnumerable<CatalogItem> ItemsOfKind(ProcessCatalog catalog, CatalogType kind) => kind switch
    {
        CatalogType.Tool => catalog.Tools.Select(tool => new CatalogItem(CatalogType.Tool, tool.Name, tool.Description, catalog.ProcessId, catalog.Instance, Tool: tool)),
        CatalogType.Resource => catalog.Resources.Select(resource => new CatalogItem(CatalogType.Resource, resource.Uri, resource.Description, catalog.ProcessId, catalog.Instance, Resource: resource)),
        CatalogType.ResourceTemplate => catalog.ResourceTemplates.Select(template => new CatalogItem(CatalogType.ResourceTemplate, template.UriTemplate, template.Description, catalog.ProcessId, catalog.Instance, ResourceTemplate: template)),
        _ => []
    };
}
