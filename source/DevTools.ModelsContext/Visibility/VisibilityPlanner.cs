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

    public VisibilityCheck Check(VisibilityRequest request)
    {
        var builder = new Builder(request.Action, collect: false);
        var reason = Fill(builder, request);
        return new VisibilityCheck(reason, reason is null && builder.TouchesLink,
            reason is null && builder.LinkFilters.Count > 0);
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

        var filters = AddResultFilters(builder, request.Filter);
        if (filters is not null)
        {
            return filters;
        }

        return builder.IsEmpty ? VisibilityReasons.Nothing : builder.LinkRuleRefusal();
    }

    private string? AddRow(Builder builder, TreeRow row)
    {
        return row.Kind switch
        {
            TreeRowKind.CurrentModelHeader => VisibilityReasons.HeaderHasNoActions,
            TreeRowKind.MorePage => VisibilityReasons.PageRowHasNoActions,
            TreeRowKind.Category => AddGroupEverywhere(builder, row),
            _ => AddDocumentRow(builder, row)
        };
    }

    private string? AddDocumentRow(Builder builder, TreeRow row)
    {
        if (row.Document is not { } key)
        {
            return VisibilityReasons.NotSupported;
        }

        if (documents(key) is not { IsLoaded: true } info)
        {
            return VisibilityReasons.NotLoaded;
        }

        return row.Kind == TreeRowKind.LinkModel
            ? AddLinkModel(builder, info)
            : AddBranch(builder, row, info);
    }

    private static string? AddLinkModel(Builder builder, DocumentInfo info)
    {
        if (!info.Can(DocumentCapabilities.WholeModel))
        {
            return VisibilityReasons.NotSupported;
        }

        builder.AttachedModels.Add(info.Key);
        return null;
    }

    private string? AddBranch(Builder builder, TreeRow row, DocumentInfo info)
    {
        if (CheckDocument(builder.Action, info) is { } reason)
        {
            return reason;
        }

        if (scopes(info.Key) is not { Snapshot.IsDirty: false } scope)
        {
            return VisibilityReasons.Updating;
        }

        if (builder.IsOverride && !info.Key.IsCurrentModel)
        {
            return AddLinkRuleRow(builder, row, info.Key, scope);
        }

        builder.Add(info.Key, builder.Action == VisibilityAction.Show && row.Kind != TreeRowKind.Instance
            ? () => scope.Snapshot.IdsUnder(row.Path)
            : () => scope.IdsOf(row));
        return null;
    }

    private static string? AddLinkRuleRow(Builder builder, TreeRow row, DocumentKey key, DocumentScope scope)
    {
        if (!LinkRules.IsRuleRow(row))
        {
            return VisibilityReasons.LinkOverrideNeedsRule;
        }

        var snapshot = scope.Snapshot;
        builder.AddLinkRule(key, LinkRules.CategoryOf(snapshot, row), LinkRules.RuleOf(snapshot, row),
            () => scope.IdsOf(row));
        return null;
    }

    private string? AddGroupEverywhere(Builder builder, TreeRow row)
    {
        var current = row.Document is { } key ? documents(key.CurrentModel) : null;
        if (current is null || !current.Can(DocumentCapabilities.HideGroupEverywhere))
        {
            return VisibilityReasons.NotSupported;
        }

        builder.Groups[row.Id] = row.Text;
        return null;
    }

    private string? AddElement(Builder builder, ElementRef element, FilterDefinition? filter)
    {
        var info = documents(element.Document);
        if (info is null || !info.IsLoaded)
        {
            return VisibilityReasons.NotLoaded;
        }

        if (CheckDocument(builder.Action, info) is { } reason)
        {
            return reason;
        }

        if (builder.IsOverride && !element.Document.IsCurrentModel)
        {
            if (filter is null)
            {
                return VisibilityReasons.LinkOverrideNeedsRule;
            }

            builder.AddResultElement(element);
            return null;
        }

        builder.Add(element.Document, () => [element.Id]);
        return null;
    }

    private string? AddResultFilters(Builder builder, FilterDefinition? filter)
    {
        if (filter is null || builder.ResultElements.Count == 0)
        {
            return null;
        }

        if (LinkRules.Refusal(filter.Root) is { } refusal)
        {
            return refusal;
        }

        foreach (var pair in builder.ResultElements)
        {
            if (scopes(pair.Key) is not { Snapshot.IsDirty: false } scope)
            {
                return VisibilityReasons.Updating;
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

    private static string? CheckDocument(VisibilityAction action, DocumentInfo info)
    {
        if (info.Key.IsCurrentModel)
        {
            return info.Can(DocumentCapabilities.HideById) ? null : VisibilityReasons.NotSupported;
        }

        if (action is VisibilityAction.Halftone or VisibilityAction.Color)
        {
            return info.Can(DocumentCapabilities.LinkFilters) ? null : VisibilityReasons.LinkFiltersUnavailable;
        }

        if (!info.Can(DocumentCapabilities.ReferenceCommands))
        {
            return VisibilityReasons.ReferenceCommandsUnavailable;
        }

        return action == VisibilityAction.Show && !info.Can(DocumentCapabilities.ReferenceUnhide)
            ? VisibilityReasons.ShowFromReveal
            : null;
    }

    private sealed class Builder(VisibilityAction action, bool collect)
    {
        private readonly Dictionary<DocumentKey, HashSet<long>> _ids = [];
        private readonly HashSet<DocumentKey> _touched = [];
        private readonly Dictionary<(DocumentKey Document, long Category), LinkRuleGroup> _rules = [];
        private readonly List<LinkFilterStep> _results = [];

        public VisibilityAction Action { get; } = action;

        public bool IsOverride => Action is VisibilityAction.Halftone or VisibilityAction.Color;

        public Dictionary<long, string> Groups { get; } = [];

        public List<DocumentKey> AttachedModels { get; } = [];

        public Dictionary<DocumentKey, List<long>> ResultElements { get; } = [];

        public List<LinkFilterStep> LinkFilters => [.. _rules.Select(pair => new LinkFilterStep(pair.Key.Document,
            [.. pair.Value.Ids.OrderBy(id => id)],
            new FilterDefinition([pair.Key.Category], LinkRules.Combine(pair.Value.Rules)))), .. _results];

        public bool IsEmpty => Groups.Count == 0 && AttachedModels.Count == 0 && _touched.Count == 0
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

        public void AddResultElement(ElementRef element)
        {
            if (!ResultElements.TryGetValue(element.Document, out var ids))
            {
                ids = [];
                ResultElements.Add(element.Document, ids);
            }

            ids.Add(element.Id);
        }

        public void AddLinkRule(DocumentKey key, long category, FilterNode? rule, Func<IEnumerable<long>> ids)
        {
            if (!_rules.TryGetValue((key, category), out var group))
            {
                group = new LinkRuleGroup();
                _rules.Add((key, category), group);
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
            return count > VisibilityReasons.LinkFilterCap ? VisibilityReasons.LinkFilterTooMany : null;
        }

        public VisibilityPlan Build(RgbColor? color)
        {
            var steps = new List<VisibilityStep>();
            steps.AddRange(Groups.Select(pair => new GroupEverywhereStep(pair.Key, pair.Value)));
            steps.AddRange(AttachedModels.Distinct().Select(key => new AttachedModelStep(key)));
            foreach (var pair in _ids.Where(pair => pair.Value.Count > 0).OrderBy(pair => !pair.Key.IsCurrentModel))
            {
                var ids = pair.Value.OrderBy(id => id).ToList();
                steps.Add(StepOf(pair.Key, ids));
            }

            steps.AddRange(LinkFilters);
            return new VisibilityPlan(Action, color, steps, null);
        }

        private VisibilityStep StepOf(DocumentKey key, List<long> ids)
        {
            if (key.IsCurrentModel)
            {
                return new ElementsStep(key, ids);
            }

            return Action == VisibilityAction.Isolate
                ? new LinkIsolateStep(key, ids)
                : new ReferenceCommandStep(key, ids);
        }
    }

    private sealed class LinkRuleGroup
    {
        public List<FilterNode?> Rules { get; } = [];

        public HashSet<long> Ids { get; } = [];
    }
}
