using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Engine;

public sealed class VisibilityPlanner(
    Func<DocumentKey, DocumentInfo?> documents,
    Func<DocumentKey, DocumentSnapshot?> snapshots)
{
    public VisibilityPlan Plan(VisibilityRequest request)
    {
        var builder = new Builder(request.Action);
        foreach (var row in request.Rows)
        {
            var reason = AddRow(builder, row);
            if (reason is not null)
            {
                return VisibilityPlan.Disabled(request.Action, reason);
            }
        }

        foreach (var element in request.Elements)
        {
            var reason = AddElement(builder, element);
            if (reason is not null)
            {
                return VisibilityPlan.Disabled(request.Action, reason);
            }
        }

        return builder.IsEmpty
            ? VisibilityPlan.Disabled(request.Action, VisibilityReasons.Nothing)
            : builder.Build(request.Color);
    }

    private static bool IsAppearance(VisibilityAction action)
    {
        return action is VisibilityAction.Halftone or VisibilityAction.Color;
    }

    private string? AddRow(Builder builder, TreeRow row)
    {
        switch (row.Kind)
        {
            case TreeRowKind.AllModelsHeader:
            case TreeRowKind.CurrentModelHeader:
                return VisibilityReasons.HeaderHasNoActions;
            case TreeRowKind.MorePage:
                return VisibilityReasons.PageRowHasNoActions;
            case TreeRowKind.AllModelsGroup:
                return AddGroupEverywhere(builder, row);
        }

        if (row.Document is not { } key)
        {
            return VisibilityReasons.NotSupported;
        }

        var info = documents(key);
        if (info is null || !info.IsLoaded)
        {
            return VisibilityReasons.NotLoaded;
        }

        if (row.Kind == TreeRowKind.LinkModel)
        {
            if (!info.Can(DocumentCapabilities.WholeModel))
            {
                return VisibilityReasons.NotSupported;
            }

            builder.AttachedModels.Add(key);
            return null;
        }

        var check = CheckDocument(builder.Action, info);
        if (check is not null)
        {
            return check;
        }

        if (snapshots(key) is not { IsDirty: false } snapshot)
        {
            return VisibilityReasons.Updating;
        }

        builder.Ids(key).UnionWith(snapshot.IdsUnder(row.Path));
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

        var check = CheckDocument(builder.Action, info);
        if (check is not null)
        {
            return check;
        }

        builder.Ids(element.Document).Add(element.Id);
        return null;
    }

    private static string? CheckDocument(VisibilityAction action, DocumentInfo info)
    {
        if (info.Key.IsCurrentModel)
        {
            return info.Can(DocumentCapabilities.HideById) ? null : VisibilityReasons.NotSupported;
        }

        if (IsAppearance(action))
        {
            return VisibilityReasons.LinkChildOnlyShowIsolate;
        }

        return info.Can(DocumentCapabilities.ReferenceCommands)
            ? null
            : VisibilityReasons.ReferenceCommandsUnavailable;
    }

    private sealed class Builder(VisibilityAction action)
    {
        private readonly Dictionary<DocumentKey, HashSet<long>> _ids = [];

        public VisibilityAction Action { get; } = action;

        public Dictionary<long, string> Groups { get; } = [];

        public List<DocumentKey> AttachedModels { get; } = [];

        public bool IsEmpty => Groups.Count == 0 && AttachedModels.Count == 0 && _ids.Values.All(ids => ids.Count == 0);

        public HashSet<long> Ids(DocumentKey key)
        {
            if (!_ids.TryGetValue(key, out var ids))
            {
                ids = [];
                _ids.Add(key, ids);
            }

            return ids;
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
