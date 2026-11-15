using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Engine;

public sealed class VisibilityPlanner(
    Func<DocumentKey, DocumentInfo?> documents,
    Func<DocumentKey, DocumentSnapshot?> snapshots)
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
        return new VisibilityCheck(reason, reason is null && builder.TouchesLink);
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
            var reason = AddElement(builder, element);
            if (reason is not null)
            {
                return reason;
            }
        }

        return builder.IsEmpty ? VisibilityReasons.Nothing : null;
    }

    private string? AddRow(Builder builder, TreeRow row)
    {
        return row.Kind switch
        {
            TreeRowKind.AllModelsHeader or TreeRowKind.CurrentModelHeader => VisibilityReasons.HeaderHasNoActions,
            TreeRowKind.MorePage => VisibilityReasons.PageRowHasNoActions,
            TreeRowKind.AllModelsGroup => AddGroupEverywhere(builder, row),
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

        if (snapshots(info.Key) is not { IsDirty: false } snapshot)
        {
            return VisibilityReasons.Updating;
        }

        builder.Add(info.Key, () => snapshot.IdsUnder(row.Path));
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

    private string? AddElement(Builder builder, ElementRef element)
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

        builder.Add(element.Document, () => [element.Id]);
        return null;
    }

    private static string? CheckDocument(VisibilityAction action, DocumentInfo info)
    {
        if (info.Key.IsCurrentModel)
        {
            return info.Can(DocumentCapabilities.HideById) ? null : VisibilityReasons.NotSupported;
        }

        if (action is not (VisibilityAction.Hide or VisibilityAction.Show))
        {
            return VisibilityReasons.LinkChildOnlyHideShow;
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

        public VisibilityAction Action { get; } = action;

        public Dictionary<long, string> Groups { get; } = [];

        public List<DocumentKey> AttachedModels { get; } = [];

        public bool IsEmpty => Groups.Count == 0 && AttachedModels.Count == 0 && _touched.Count == 0
                               && _ids.Values.All(ids => ids.Count == 0);

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

        public VisibilityPlan Build(RgbColor? color)
        {
            var steps = new List<VisibilityStep>();
            steps.AddRange(Groups.Select(pair => new GroupEverywhereStep(pair.Key, pair.Value)));
            steps.AddRange(AttachedModels.Distinct().Select(key => new AttachedModelStep(key)));
            foreach (var pair in _ids.Where(pair => pair.Value.Count > 0).OrderBy(pair => !pair.Key.IsCurrentModel))
            {
                var ids = pair.Value.OrderBy(id => id).ToList();
                steps.Add(pair.Key.IsCurrentModel
                    ? new ElementsStep(pair.Key, ids)
                    : new ReferenceCommandStep(pair.Key, ids));
            }

            return new VisibilityPlan(Action, color, steps, null);
        }
    }
}
