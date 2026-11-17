using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Engine;

public static class FilterScopes
{
    public static FilterSet? ForDocument(FilterSet root, SourceId document, ICollection<SourceId> known)
    {
        return Prune(root, document, known);
    }

    public static bool IsUnresolved(FilterScope? scope, ICollection<SourceId> known)
    {
        return scope is not null && scope.Documents.Any(source => !known.Contains(source));
    }

    public static bool Excludes(FilterScope? scope, SourceId document)
    {
        return scope is { Documents.Count: > 0 } && !scope.Documents.Contains(document);
    }

    private static FilterSet? Prune(FilterSet set, SourceId document, ICollection<SourceId> known)
    {
        if (IsUnresolved(set.Scope, known) || Excludes(set.Scope, document))
        {
            return null;
        }

        var children = PruneChildren(set, document, known);
        return children is null || LostEveryChild(set, children) ? null : set with { Children = children };
    }

    private static List<FilterNode>? PruneChildren(FilterSet set, SourceId document, ICollection<SourceId> known)
    {
        var children = new List<FilterNode>(set.Children.Count);
        foreach (var child in set.Children)
        {
            var kept = child is FilterSet nested ? Prune(nested, document, known) : child;
            if (kept is not null)
            {
                children.Add(kept);
            }
            else if (set.Match == SetMatch.All)
            {
                return null;
            }
        }

        return children;
    }

    private static bool LostEveryChild(FilterSet set, List<FilterNode> children)
    {
        return set.Match == SetMatch.Any && children.Count == 0 && set.Children.Count > 0;
    }
}

public sealed record EffectiveScope(IReadOnlyCollection<SourceId> Documents, IReadOnlyCollection<long>? Categories)
{
    public static EffectiveScope Empty { get; } = new([], null);

    public bool AllCategories => Categories is null;

    public bool IsEmpty => Documents.Count == 0 || Categories is { Count: 0 };

    public EffectiveScope Narrow(FilterScope? scope)
    {
        if (scope is null || scope.IsSame)
        {
            return this;
        }

        var documents = scope.Documents.Count == 0
            ? Documents
            : Documents.Where(scope.Documents.Contains).ToList();
        var categories = scope.Categories.Count == 0
            ? Categories
            : Categories is null
                ? scope.Categories
                : Categories.Where(scope.Categories.Contains).ToList();
        return new EffectiveScope(documents, categories);
    }
}
