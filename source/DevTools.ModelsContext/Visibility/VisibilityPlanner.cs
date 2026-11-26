using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Scope;

namespace DevTools.ModelsContext.Visibility;

public sealed class VisibilityPlanner(
    Func<DocumentKey, DocumentInfo?> documents,
    Func<DocumentKey, DocumentScope?> scopes)
{
    public VisibilityPlan Plan(VisibilityRequest request)
    {
        var builder = new Builder(request.Action, collect: true);
        var reason = Fill(builder, request);
        return reason is null
            ? builder.Build(request.Color)
            : VisibilityPlan.Disabled(request.Action, reason);
    }

    public ActionState Check(VisibilityRequest request)
    {
        var builder = new Builder(request.Action, collect: false);
        var reason = Fill(builder, request);
        return reason is null
            ? new ActionState(null, ActionState.NoteOf(builder.LinkFilters.Count > 0, builder.TouchesLink))
            : new ActionState(reason);
    }

    private string? Fill(Builder builder, VisibilityRequest request)
    {
        foreach (var row in request.Rows)
        {
            var reason = AddRow(builder, row);
            if (reason is not null)
            {
                return reason;
            }
        }

        foreach (var element in request.Elements)
        {
            var reason = AddElement(builder, element, request.Filter);
            if (reason is not null)
            {
                return reason;
            }
        }

        var filters = AddLinkResults(builder, request.Filter);
        if (filters is not null)
        {
            return filters;
        }

        return builder.IsEmpty ? VisibilityText.Nothing : builder.LinkRuleRefusal();
    }

    private string? AddRow(Builder builder, TreeRow row)
    {
        return row.Kind switch
        {
            TreeRowKind.MorePage => VisibilityText.PageRowHasNoActions,
            TreeRowKind.Category => AddCategory(builder, row),
            _ => AddDocumentRow(builder, row)
        };
    }

    private string? AddDocumentRow(Builder builder, TreeRow row)
    {
        if (row.Document is not { } key)
        {
            return VisibilityText.NotSupported;
        }

        if (documents(key) is not { IsLoaded: true } info)
        {
            return VisibilityText.NotLoaded;
        }

        return row.Kind == TreeRowKind.LinkModel
            ? AddLinkInstance(builder, info)
            : AddBranch(builder, row, info);
    }

    private static string? AddLinkInstance(Builder builder, DocumentInfo info)
    {
        if (!info.Can(DocumentCapabilities.LinkInstance))
        {
            return VisibilityText.NotSupported;
        }

        builder.LinkInstances.Add(info.Key);
        return null;
    }

    private string? AddBranch(Builder builder, TreeRow row, DocumentInfo info)
    {
        if (RefusalOf(builder.Action, info) is { } reason)
        {
            return reason;
        }

        if (scopes(info.Key) is not { Snapshot.IsDirty: false } scope)
        {
            return VisibilityText.Updating;
        }

        if (builder.IsOverride && !info.Key.IsCurrentModel)
        {
            return AddLinkRuleRow(builder, row, info.Key, scope);
        }

        builder.Add(info.Key, builder.Action == VisibilityAction.Show && row.Kind != TreeRowKind.Instance
            ? () => BranchesOf(scope.Snapshot, row).SelectMany(scope.Snapshot.IdsUnder)
            : () => scope.IdsOf(row));
        return null;
    }

    private static IReadOnlyList<RowPath> BranchesOf(DocumentSnapshot snapshot, TreeRow row)
    {
        return row.Kind == TreeRowKind.CurrentModelHeader ? row.Branches : snapshot.WithSubgroups(row.Path);
    }

    private static string? AddLinkRuleRow(Builder builder, TreeRow row, DocumentKey key, DocumentScope scope)
    {
        if (!LinkRules.IsRuleRow(row))
        {
            return VisibilityText.LinkOverrideNeedsRule;
        }

        var snapshot = scope.Snapshot;
        builder.AddLinkRule(key, LinkRules.CategoriesOf(snapshot, row), LinkRules.RuleOf(snapshot, row),
            () => scope.IdsOf(row));
        return null;
    }

    private string? AddCategory(Builder builder, TreeRow row)
    {
        var current = row.Document is { } key ? documents(key.CurrentModel) : null;
        if (current is null || !current.Can(DocumentCapabilities.Categories))
        {
            return VisibilityText.NotSupported;
        }

        builder.Categories[row.Id] = row.Text;
        return null;
    }

    private string? AddElement(Builder builder, ElementRef element, FilterDefinition? filter)
    {
        var info = documents(element.Document);
        if (info is null || !info.IsLoaded)
        {
            return VisibilityText.NotLoaded;
        }

        if (RefusalOf(builder.Action, info) is { } reason)
        {
            return reason;
        }

        if (builder.IsOverride && !element.Document.IsCurrentModel)
        {
            if (filter is null)
            {
                return VisibilityText.LinkOverrideNeedsRule;
            }

            builder.AddLinkResult(element);
            return null;
        }

        builder.Add(element.Document, () => [element.Id]);
        return null;
    }

    private string? AddLinkResults(Builder builder, FilterDefinition? filter)
    {
        if (filter is null || builder.LinkResults.Count == 0)
        {
            return null;
        }

        if (LinkRules.Refusal(filter.Root) is { } refusal)
        {
            return refusal;
        }

        foreach (var pair in builder.LinkResults)
        {
            if (scopes(pair.Key) is not { Snapshot.IsDirty: false } scope)
            {
                return VisibilityText.Updating;
            }

            var categories = filter.Groups.Count > 0 ? filter.Groups : CategoriesOf(scope.Snapshot, pair.Value);
            builder.AddLinkFilter(pair.Key, new FilterDefinition(categories, filter.Root), pair.Value);
        }

        return null;
    }

    private static List<long> CategoriesOf(DocumentSnapshot snapshot, IEnumerable<long> ids)
    {
        var categories = new HashSet<long>();
        foreach (var id in ids)
        {
            if (snapshot.TryGetRow(id, out var row))
            {
                categories.Add(snapshot.Groups[row.GroupIndex].GroupId);
            }
        }

        return [.. categories.OrderBy(id => id)];
    }

    private static string? RefusalOf(VisibilityAction action, DocumentInfo info)
    {
        if (info.Key.IsCurrentModel)
        {
            return info.Can(DocumentCapabilities.Elements) ? null : VisibilityText.NotSupported;
        }

        if (action is VisibilityAction.Halftone or VisibilityAction.Color)
        {
            return info.Can(DocumentCapabilities.LinkFilters) ? null : VisibilityText.LinkFiltersUnavailable;
        }

        if (!info.Can(DocumentCapabilities.LinkElements))
        {
            return VisibilityText.LinkElementsUnavailable;
        }

        return action == VisibilityAction.Show && !info.Can(DocumentCapabilities.LinkShow)
            ? VisibilityText.ShowFromReveal
            : null;
    }

    private sealed class Builder(VisibilityAction action, bool collect)
    {
        private readonly Dictionary<DocumentKey, HashSet<long>> _ids = [];
        private readonly HashSet<DocumentKey> _touched = [];
        private readonly Dictionary<(DocumentKey Document, string Categories), LinkRuleSet> _rules = [];
        private readonly List<LinkFilterStep> _results = [];

        public VisibilityAction Action { get; } = action;

        public bool IsOverride => Action is VisibilityAction.Halftone or VisibilityAction.Color;

        public Dictionary<long, string> Categories { get; } = [];

        public List<DocumentKey> LinkInstances { get; } = [];

        public Dictionary<DocumentKey, List<long>> LinkResults { get; } = [];

        public List<LinkFilterStep> LinkFilters => [.. _rules.Select(pair => new LinkFilterStep(pair.Key.Document,
            [.. pair.Value.Ids.OrderBy(id => id)],
            new FilterDefinition(pair.Value.Categories, LinkRules.Combine(pair.Value.Rules)))), .. _results];

        public bool IsEmpty => Categories.Count == 0 && LinkInstances.Count == 0 && _touched.Count == 0
                               && _ids.Values.All(ids => ids.Count == 0) && _rules.Count == 0 && _results.Count == 0;

        public bool TouchesLink => _touched.Any(key => !key.IsCurrentModel);

        public void Add(DocumentKey key, Func<IEnumerable<long>> ids)
        {
            if (!collect)
            {
                _touched.Add(key);
                return;
            }

            if (!_ids.TryGetValue(key, out var set))
            {
                set = [];
                _ids.Add(key, set);
            }

            set.UnionWith(ids());
        }

        public void AddLinkResult(ElementRef element)
        {
            if (!LinkResults.TryGetValue(element.Document, out var ids))
            {
                ids = [];
                LinkResults.Add(element.Document, ids);
            }

            ids.Add(element.Id);
        }

        public void AddLinkRule(
            DocumentKey key,
            IReadOnlyList<long> categories,
            FilterNode? rule,
            Func<IEnumerable<long>> ids)
        {
            var name = string.Join(",", categories);
            if (!_rules.TryGetValue((key, name), out var group))
            {
                group = new LinkRuleSet(categories);
                _rules.Add((key, name), group);
            }

            group.Rules.Add(rule);
            if (collect)
            {
                group.Ids.UnionWith(ids());
            }
        }

        public void AddLinkFilter(DocumentKey key, FilterDefinition filter, IReadOnlyList<long> ids)
        {
            _results.Add(new LinkFilterStep(key, collect ? [.. ids.OrderBy(id => id)] : [], filter));
        }

        public string? LinkRuleRefusal()
        {
            var count = LinkFilters.Sum(step => LinkRules.CountOf(step.Filter.Root));
            return count > LinkRules.MaxRules ? VisibilityText.LinkFilterTooMany : null;
        }

        public VisibilityPlan Build(RgbColor? color)
        {
            var steps = new List<PlanStep>();
            steps.AddRange(Categories.Select(pair => new CategoryStep(pair.Key, pair.Value)));
            steps.AddRange(LinkInstances.Distinct().Select(key => new LinkInstanceStep(key)));
            foreach (var pair in _ids.Where(pair => pair.Value.Count > 0).OrderBy(pair => !pair.Key.IsCurrentModel))
            {
                var ids = pair.Value.OrderBy(id => id).ToList();
                steps.Add(StepOf(pair.Key, ids));
            }

            steps.AddRange(LinkFilters);
            return new VisibilityPlan(Action, color, steps, null);
        }

        private PlanStep StepOf(DocumentKey key, List<long> ids)
        {
            if (key.IsCurrentModel)
            {
                return new ElementsStep(key, ids);
            }

            return Action == VisibilityAction.Isolate
                ? new LinkIsolateStep(key, ids)
                : new LinkElementsStep(key, ids);
        }
    }

    private sealed class LinkRuleSet(IReadOnlyList<long> categories)
    {
        public IReadOnlyList<long> Categories { get; } = categories;

        public List<FilterNode?> Rules { get; } = [];

        public HashSet<long> Ids { get; } = [];
    }
}
