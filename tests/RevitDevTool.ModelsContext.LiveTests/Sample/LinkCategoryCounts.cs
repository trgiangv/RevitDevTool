using RevitDevTool.Tools.ModelsContext.Visibility;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed class LinkCategoryCounts
{
    private const string CurrentModel = "Current model";

    private readonly Dictionary<string, Dictionary<string, int>> _models;

    private LinkCategoryCounts(Dictionary<string, Dictionary<string, int>> models)
    {
        _models = models;
    }

    public static LinkCategoryCounts Read(Document host, View view)
    {
        var models = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal)
        {
            [CurrentModel] = CountOf(new FilteredElementCollector(host, view.Id))
        };
        foreach (var instance in HostViewLinks(host, view))
        {
            if (LinkElements.InView(host, view, instance.Id) is { } collector)
            {
                models[instance.Name] = CountOf(collector);
            }
        }

        return new LinkCategoryCounts(models);
    }

    public int Of(string category)
    {
        return _models.Values.Sum(counts => counts.TryGetValue(category, out var count) ? count : 0);
    }

    public List<string> StrayOf(IReadOnlyCollection<string> allowed)
    {
        return _models.SelectMany(model => model.Value
                .Where(pair => !allowed.Contains(pair.Key))
                .Select(pair => $"{Short(model.Key)}: {pair.Key}={pair.Value}"))
            .ToList();
    }

    public bool SameAs(LinkCategoryCounts other)
    {
        return _models.Count == other._models.Count
               && _models.All(pair => other._models.TryGetValue(pair.Key, out var counts)
                                      && pair.Value.Count == counts.Count
                                      && pair.Value.All(entry => counts.TryGetValue(entry.Key, out var count) && count == entry.Value));
    }

    public override string ToString()
    {
        return string.Join("; ", _models.Select(pair => $"{Short(pair.Key)} {pair.Value.Values.Sum():N0}"));
    }

    private static Dictionary<string, int> CountOf(FilteredElementCollector collector)
    {
        return collector.WhereElementIsNotElementType()
            .Where(element => element.Category is not null)
            .GroupBy(element => element.Category!.Parent?.Name ?? element.Category!.Name)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    private static string Short(string name)
    {
        var index = name.IndexOf(':');
        return index < 0 ? name : name.Substring(0, index).Trim();
    }

    private static IEnumerable<RevitLinkInstance> HostViewLinks(Document host, View view)
    {
        return new FilteredElementCollector(host).OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .Where(instance => instance.GetLinkDocument() is not null
                               && !instance.IsHidden(view)
                               && RevitVisibilityExplainer.DisplayModeOf(view, instance) is null);
    }
}
