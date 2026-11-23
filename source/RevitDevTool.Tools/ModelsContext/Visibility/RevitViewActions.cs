using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Visibility;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.Geometry;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Pane;

namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal sealed class RevitViewActions : IViewActions
{
    public const string Needs3DView = "Section box needs a 3D view.";
    public const string NoTemporaryProperties = "This view cannot take temporary overrides.";
    public const string ModelChanged = "The active model changed.";
    public const string NothingToFrame = "Nothing to frame.";

    private const string TransactionName = "Model Context";
    private const string IsolateName = "Model Context Isolate";
    private const double ZoomFactor = 1.25;
    private const double BoxFactor = 1.0;

    private readonly Dictionary<(string Model, ElementId View), Dictionary<ElementId, OverrideGraphicSettings>> _prior = [];
    private readonly Dictionary<(string Model, ElementId View), HashSet<ElementId>> _isolated = [];
    private string? _notice;

    public string? TakeNotice()
    {
        var notice = _notice;
        _notice = null;
        return notice;
    }

    public Task<IReadOnlyList<ElementRef>> ExecuteAsync(VisibilityPlan plan, CancellationToken cancellationToken)
    {
        _notice = null;
        if (plan.DisabledReason is { } reason)
        {
            throw new InvalidOperationException(reason);
        }

        if (plan.Steps.OfType<LinkFilterStep>().Select(step => LinkRules.Refusal(step.Filter.Root))
                .FirstOrDefault(refusal => refusal is not null) is { } refused)
        {
            throw new InvalidOperationException(refused);
        }

        if (plan.Steps.Any(step => step is LinkIsolateStep))
        {
            return IsolateWithLinksAsync(plan, cancellationToken);
        }

        if (!plan.UsesReferenceCommands)
        {
            return RevitProfiler.RunAsync<IReadOnlyList<ElementRef>>("Visibility", application =>
            {
                Execute(application, plan);
                return [];
            }, cancellationToken);
        }

        if (plan.Action != VisibilityAction.Hide)
        {
            throw new InvalidOperationException(VisibilityReasons.LinkOverrideNeedsRule);
        }

        return HideWithLinksAsync(plan, cancellationToken);
    }

    public Task ShowLinkedAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        var linked = elements.Where(element => !element.Document.IsCurrentModel).ToList();
        return linked.Count == 0
            ? Task.CompletedTask
            : RevitProfiler.RunAsync("Show links", application => LinkedHideShow.Show(application, linked),
                cancellationToken);
    }

    public Task KeepAsync(CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Keep", application =>
        {
            var (active, view) = RequireView(application);
            if (!view.IsTemporaryHideIsolateActive())
            {
                return;
            }

            RevitErrors.EnsureEditable(active.Document);
            using var transaction = new Transaction(active.Document, TransactionName);
            transaction.Start();
            view.ConvertTemporaryHideIsolateToPermanent();
            transaction.Commit();
        }, cancellationToken);
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Reset", application =>
        {
            var (active, view) = RequireView(application);
            var hideIsolate = view.IsTemporaryHideIsolateActive();
            var properties = IsPropertiesModeActive(view);
            var key = KeyOf(active.Document, view);
            _prior.TryGetValue(key, out var prior);
            var filters = LinkFilters.HasAny(active.Document, view);
            if (!hideIsolate && !properties && prior is null && !filters)
            {
                return;
            }

            RevitErrors.EnsureEditable(active.Document);
            using var transaction = new Transaction(active.Document, TransactionName);
            transaction.Start();
            var failed = LinkFilters.Remove(active.Document, view);
            if (hideIsolate)
            {
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
            }

            if (properties)
            {
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryViewProperties);
            }

            foreach (var pair in prior ?? [])
            {
                if (active.Document.GetElement(pair.Key) is not null)
                {
                    view.SetElementOverrides(pair.Key, pair.Value);
                }
            }

            transaction.Commit();
            _prior.Remove(key);
            _isolated.Remove(key);
            if (failed is not null)
            {
                throw new InvalidOperationException(failed);
            }
        }, cancellationToken);
    }

    public Task ZoomAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Zoom", application =>
        {
            var (active, view) = RequireView(application);
            if (!TryFrame(active.Document, view, elements, ZoomFactor, out var min, out var max))
            {
                throw new InvalidOperationException(NothingToFrame);
            }

            active.GetOpenUIViews().FirstOrDefault(open => open.ViewId == view.Id)?.ZoomAndCenterRectangle(min, max);
        }, cancellationToken);
    }

    public Task SectionBoxAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Section box", application =>
        {
            var (active, view) = RequireView(application);
            if (view is not View3D view3D)
            {
                throw new InvalidOperationException(Needs3DView);
            }

            if (!TryFrame(active.Document, view, elements, BoxFactor, out var min, out var max))
            {
                throw new InvalidOperationException(NothingToFrame);
            }

            ElementBox.Expand(min, max, ZoomFactor, out var boxMin, out var boxMax);
            RevitErrors.EnsureEditable(active.Document);
            using var transaction = new Transaction(active.Document, TransactionName);
            transaction.Start();
            view3D.SetSectionBox(new BoundingBoxXYZ { Min = boxMin, Max = boxMax });
            view3D.IsSectionBoxActive = true;
            transaction.Commit();
        }, cancellationToken);
    }

    public Task<TemporaryState?> ReadTemporaryStateAsync(CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync<TemporaryState?>("Temporary state", application =>
        {
            if (application.ActiveUIDocument is not { ActiveView: { } view } active)
            {
                return null;
            }

            return new TemporaryState(ViewInfoOf(active.Document, view), view.IsTemporaryHideIsolateActive(),
                IsPropertiesModeActive(view));
        }, cancellationToken);
    }

    public async Task<IReadOnlyCollection<long>> GetIdsHiddenPermanentlyOrTemporarilyAsync(
        DocumentKey document,
        IReadOnlyCollection<long> candidates,
        CancellationToken cancellationToken)
    {
        if (!document.IsCurrentModel || candidates.Count == 0)
        {
            return [];
        }

        var ids = candidates.ToArray();
        var hidden = new HashSet<long>();
        var stats = ProfileStats.Start("Hidden");
        var index = 0;
        while (index < ids.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = index;
            var (next, found) = await RevitProfiler.RunAsync("Hidden slice", application =>
            {
                if (!DocumentRegistry.IsActive(application, document)
                    || application.ActiveUIDocument?.ActiveView is not { } view)
                {
                    return (ids.Length, new List<long>());
                }

                var model = application.ActiveUIDocument.Document;
                return HiddenSlice(model, view, ids, start, IsolatedOutOf(model, view));
            }, cancellationToken, stats).ConfigureAwait(false);
            hidden.UnionWith(found);
            index = next;
        }

        stats?.Flush(document, ids.Length);
        return hidden;
    }

    internal static ViewInfo ViewInfoOf(Document model, View view)
    {
        return new ViewInfo(DocumentRegistry.KeyOf(model),
            view.Id.ToString(), view.Name, ElementDrawing.KindOf(view));
    }

    private HashSet<ElementId> IsolatedOutOf(Document model, View view)
    {
        return IsPropertiesModeActive(view) && _isolated.TryGetValue(KeyOf(model, view), out var hidden) ? hidden : [];
    }

    private static (int Next, List<long> Hidden) HiddenSlice(
        Document model,
        View view,
        long[] ids,
        int start,
        HashSet<ElementId> isolatedOut)
    {
        var temporary = view.IsTemporaryHideIsolateActive();
        var hidden = new List<long>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var index = start;
        while (index < ids.Length && index - start < ElementReader.SliceSize && watch.Elapsed < ElementReader.SliceTime)
        {
            if (model.GetElement(ids[index].ToElementId()) is { } element && IsHidden(view, element, temporary, isolatedOut))
            {
                hidden.Add(ids[index]);
            }

            index++;
        }

        return (index, hidden);
    }

    private static bool IsHidden(View view, Element element, bool temporary, HashSet<ElementId> isolatedOut)
    {
        return element.IsHidden(view)
               || (temporary && !view.IsElementVisibleInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate, element.Id))
               || (element.Category is { } category && isolatedOut.Contains(category.Id));
    }

    private Task<IReadOnlyList<ElementRef>> HideWithLinksAsync(VisibilityPlan plan, CancellationToken cancellationToken)
    {
        var apiSteps = plan.Steps.Where(step => step is not ReferenceCommandStep).ToList();
        var linked = plan.Steps.OfType<ReferenceCommandStep>()
            .SelectMany(step => step.Ids.Select(id => new ElementRef(step.Document, id)))
            .ToList();
        return RevitProfiler.RunAsync<IReadOnlyList<ElementRef>>("Hide links", application =>
        {
            if (apiSteps.Count > 0)
            {
                Execute(application, plan with { Steps = apiSteps });
            }

            LinkedHideShow.Hide(application, linked);
            return linked;
        }, cancellationToken);
    }

    private Task<IReadOnlyList<ElementRef>> IsolateWithLinksAsync(VisibilityPlan plan, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync<IReadOnlyList<ElementRef>>("Isolate links", application =>
        {
            var (active, view) = RequireView(application);
            if (plan.ViewId is { } viewId && viewId != view.Id.ToString())
            {
                throw new InvalidOperationException(VisibilityReasons.ViewChanged);
            }

            if (view.IsInTemporaryViewMode(TemporaryViewMode.RevealHiddenElements))
            {
                throw new InvalidOperationException(VisibilityReasons.RevealOn);
            }

            var model = active.Document;
            var rest = LinkIsolation.RestOf(model, view, plan.Steps.OfType<LinkIsolateStep>());
            RevitErrors.EnsureEditable(model);
            using var group = new TransactionGroup(model, IsolateName);
            group.Start();
            try
            {
                Execute(application, plan);
                if (rest.Count > 0)
                {
                    LinkedHideShow.Hide(application, rest);
                }

                group.Assimilate();
            }
            catch
            {
                if (group.HasStarted())
                {
                    group.RollBack();
                }

                throw;
            }

            return rest;
        }, cancellationToken);
    }

    private void Execute(UIApplication application, VisibilityPlan plan)
    {
        var (active, view) = RequireView(application);
        var model = active.Document;
        var (ids, categories) = CollectTargets(model, plan.Steps);
        var filters = LinkFilters.Prepare(model, view, plan.Steps.OfType<LinkFilterStep>());
        RevitErrors.EnsureEditable(model);
        using var transaction = new Transaction(model, TransactionName);
        transaction.Start();
        ApplyAction(model, view, plan, ids, categories, filters);
        transaction.Commit();
    }

    private static (string Model, ElementId View) KeyOf(Document model, View view)
    {
        return (DocumentRegistry.ModelIdOf(model), view.Id);
    }

    private Dictionary<ElementId, OverrideGraphicSettings> PriorOf(Document model, View view)
    {
        var key = KeyOf(model, view);
        if (!_prior.TryGetValue(key, out var prior))
        {
            prior = [];
            _prior.Add(key, prior);
        }

        return prior;
    }

    private static (List<ElementId> Ids, List<ElementId> Categories) CollectTargets(
        Document model,
        IEnumerable<VisibilityStep> steps)
    {
        var modelId = DocumentRegistry.ModelIdOf(model);
        var ids = new List<ElementId>();
        var seen = new HashSet<ElementId>();
        var categories = new List<ElementId>();
        foreach (var step in steps)
        {
            switch (step)
            {
                case ElementsStep elements:
                    EnsureModel(elements.Document, modelId);
                    NestedComponents.AddWithNested(model, elements.Ids.Select(id => id.ToElementId()), ids, seen);
                    break;
                case AttachedModelStep link:
                    EnsureModel(link.Document, modelId);
                    AddLinkInstance(model, link.Document, ids);
                    break;
                case LinkIsolateStep isolate:
                    EnsureModel(isolate.Document, modelId);
                    AddLinkInstance(model, isolate.Document, ids);
                    break;
                case LinkFilterStep filter:
                    EnsureModel(filter.Document, modelId);
                    break;
                case GroupEverywhereStep group:
                    categories.Add(group.GroupId.ToElementId());
                    break;
            }
        }

        return (ids, categories);
    }

    private static void AddLinkInstance(Document model, DocumentKey link, List<ElementId> ids)
    {
        if (model.GetElement(link.LinkInstanceId.ToElementId()) is RevitLinkInstance instance)
        {
            ids.Add(instance.Id);
        }
    }

    private void ApplyAction(
        Document model,
        View view,
        VisibilityPlan plan,
        List<ElementId> ids,
        List<ElementId> categories,
        IReadOnlyList<PreparedFilter> filters)
    {
        switch (plan.Action)
        {
            case VisibilityAction.Hide:
                Hide(view, ids, categories);
                break;
            case VisibilityAction.Isolate:
                Isolate(model, view, ids, categories);
                _notice = categories.Count > 0 ? NoticeOf(model, view) : null;
                break;
            case VisibilityAction.Halftone:
            case VisibilityAction.Color:
                Override(model, view, plan, ids, categories, filters);
                break;
        }
    }

    internal static (UIDocument Active, View View) RequireView(UIApplication application)
    {
        if (application.ActiveUIDocument is not { } active)
        {
            throw new InvalidOperationException(RevitErrors.Busy);
        }

        return (active, active.ActiveView ?? throw new InvalidOperationException(RevitErrors.NoActiveView));
    }

    private static void EnsureModel(DocumentKey document, string modelId)
    {
        if (!string.Equals(document.ModelId, modelId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(ModelChanged);
        }
    }

    private static bool IsPropertiesModeActive(View view)
    {
        return view.TemporaryViewModes?.IsModeActive(TemporaryViewMode.TemporaryViewProperties) == true;
    }

    private static void Hide(View view, List<ElementId> ids, List<ElementId> categories)
    {
        if (ids.Count > 0)
        {
            view.HideElementsTemporary(ids);
        }

        var hideable = categories.Where(view.CanCategoryBeHiddenTemporary).ToList();
        if (hideable.Count > 0)
        {
            view.HideCategoriesTemporary(hideable);
        }
    }

    private void Isolate(Document model, View view, List<ElementId> ids, List<ElementId> categories)
    {
        if (ids.Count > 0)
        {
            view.IsolateElementsTemporary(ids);
        }

        if (categories.Count > 0)
        {
            EnsurePropertiesMode(view);
            _isolated[KeyOf(model, view)] = CategoryIsolation.Apply(model, view, categories);
        }
    }

    private static string? NoticeOf(Document model, View view)
    {
        var own = model.CollectElements()
            .OfClass<RevitLinkInstance>()
            .Cast<RevitLinkInstance>()
            .Count(instance => instance.GetLinkDocument() is not null
                               && !instance.IsHidden(view)
                               && RevitVisibilityExplainer.DisplayModeOf(view, instance) is not null);
        return own == 0 ? null : VisibilityReasons.LinksKeepOwnSettings;
    }

    private void Override(
        Document model,
        View view,
        VisibilityPlan plan,
        List<ElementId> ids,
        List<ElementId> categories,
        IReadOnlyList<PreparedFilter> filters)
    {
        EnsurePropertiesMode(view);
        var solid = plan.Action == VisibilityAction.Color ? SolidFillOf(model) : null;
        var prior = PriorOf(model, view);
        foreach (var id in ids)
        {
            if (!prior.ContainsKey(id))
            {
                prior.Add(id, view.GetElementOverrides(id));
            }

            view.SetElementOverrides(id, Apply(view.GetElementOverrides(id), plan, solid));
        }

        foreach (var category in categories.Where(view.IsCategoryOverridable))
        {
            view.SetCategoryOverrides(category, Apply(view.GetCategoryOverrides(category), plan, solid));
        }

        LinkFilters.Apply(model, view, filters, settings => Apply(settings, plan, solid));
    }

    private static void EnsurePropertiesMode(View view)
    {
        if (IsPropertiesModeActive(view))
        {
            return;
        }

        if (!view.CanEnableTemporaryViewPropertiesMode())
        {
            throw new InvalidOperationException(NoTemporaryProperties);
        }

        view.EnableTemporaryViewPropertiesMode(view.ViewTemplateId == ElementId.InvalidElementId
            ? view.Id
            : view.ViewTemplateId);
    }

    private static OverrideGraphicSettings Apply(OverrideGraphicSettings settings, VisibilityPlan plan, ElementId? solid)
    {
        if (plan.Action == VisibilityAction.Halftone)
        {
            return settings.SetHalftone(true);
        }

        if (plan.Color is not { } rgb)
        {
            return settings;
        }

        var color = new Color(rgb.R, rgb.G, rgb.B);
        settings.SetProjectionLineColor(color)
            .SetSurfaceForegroundPatternColor(color)
            .SetCutForegroundPatternColor(color);
        if (solid is not null)
        {
            settings.SetSurfaceForegroundPatternId(solid).SetCutForegroundPatternId(solid);
        }

        return settings;
    }

    private static ElementId? SolidFillOf(Document model)
    {
        return model.CollectElements()
            .OfClass<FillPatternElement>()
            .Cast<FillPatternElement>()
            .FirstOrDefault(pattern => pattern.GetFillPattern().IsSolidFill)
            ?.Id;
    }

    private static bool TryFrame(
        Document model,
        View view,
        IReadOnlyCollection<ElementRef> elements,
        double factor,
        out XYZ min,
        out XYZ max)
    {
        XYZ? unionMin = null;
        XYZ? unionMax = null;
        foreach (var group in DocumentRegistry.GroupByDocument(model, elements))
        {
            if (TryBoxGroup(model, view, group, out var boxMin, out var boxMax))
            {
                ElementBox.Union(ref unionMin, ref unionMax, boxMin, boxMax);
            }
        }

        if (unionMin is null || unionMax is null)
        {
            min = XYZ.Zero;
            max = XYZ.Zero;
            return false;
        }

        ElementBox.Expand(unionMin, unionMax, factor, out min, out max);
        return true;
    }

    private static bool TryBoxGroup(
        Document model,
        View view,
        IGrouping<DocumentKey, ElementRef> group,
        out XYZ min,
        out XYZ max)
    {
        var ids = group.Select(element => element.Id.ToElementId()).ToList();
        if (group.Key.IsCurrentModel)
        {
            return ElementBox.TryBox(model, ids, view, 1.0, out min, out max);
        }

        if (model.GetElement(group.Key.LinkInstanceId.ToElementId()) is RevitLinkInstance instance)
        {
            return ElementBox.TryBox(instance, ids, view, 1.0, out min, out max);
        }

        min = XYZ.Zero;
        max = XYZ.Zero;
        return false;
    }
}
