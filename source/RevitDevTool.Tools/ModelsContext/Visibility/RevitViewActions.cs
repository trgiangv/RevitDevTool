using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Visibility;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.Geometry;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Pane;

namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal sealed class RevitViewActions : IViewActions
{
    private const string TransactionName = "Model Context";
    private const string IsolateName = "Model Context Isolate";
    private const string HideName = "Model Context Hide";
    private const double ZoomFactor = 1.25;
    private const double BoxFactor = 1.0;

    private readonly Dictionary<(string Model, ElementId View), Dictionary<ElementId, OverrideGraphicSettings>> _prior = [];
    private readonly Dictionary<(string Model, ElementId View), CategoriesOff> _categoriesOff = [];

    public void Rename(string oldId, string newId)
    {
        Move(_prior, oldId, newId);
        Move(_categoriesOff, oldId, newId);
    }

    private static void Move<T>(Dictionary<(string Model, ElementId View), T> records, string oldId, string newId)
    {
        foreach (var pair in records.Where(pair => pair.Key.Model == oldId).ToList())
        {
            records.Remove(pair.Key);
            records[(newId, pair.Key.View)] = pair.Value;
        }
    }

    public Task<PlanOutcome> ExecuteAsync(VisibilityPlan plan, CancellationToken cancellationToken)
    {
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

        if (!plan.UsesLinkHide)
        {
            return RevitProfiler.RunAsync("Visibility", application => new PlanOutcome([], Execute(application, plan)),
                cancellationToken);
        }

        if (plan.Action != VisibilityAction.Hide)
        {
            throw new InvalidOperationException(VisibilityText.LinkOverrideNeedsRule);
        }

        return HideWithLinksAsync(plan, cancellationToken);
    }

    public Task ShowLinkedAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        var linked = elements.Where(element => !element.Document.IsCurrentModel).ToList();
        return linked.Count == 0
            ? Task.CompletedTask
            : RevitProfiler.RunAsync("Show links", application => LinkHideShow.Show(application, linked),
                cancellationToken);
    }

    public Task KeepAsync(string? viewId, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Keep", application =>
        {
            var (active, view) = CurrentView.Require(application, viewId);
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

    public Task ResetAsync(string? viewId, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Reset", application =>
        {
            var (active, view) = CurrentView.Require(application, viewId);
            var hideIsolate = view.IsTemporaryHideIsolateActive();
            var properties = IsPropertiesModeActive(view);
            var key = KeyOf(active.Document, view);
            _prior.TryGetValue(key, out var prior);
            var filters = LinkViewFilters.HasAny(active.Document, view);
            if (!hideIsolate && !properties && prior is null && !filters)
            {
                return;
            }

            RevitErrors.EnsureEditable(active.Document);
            using var transaction = new Transaction(active.Document, TransactionName);
            transaction.Start();
            var failed = LinkViewFilters.Remove(active.Document, view);
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
            _categoriesOff.Remove(key);
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
            var (active, view) = CurrentView.Require(application);
            if (!ViewFraming.TryFrame(active.Document, view, elements, ZoomFactor, out var min, out var max))
            {
                throw new InvalidOperationException(VisibilityText.NothingToFrame);
            }

            active.GetOpenUIViews().FirstOrDefault(open => open.ViewId == view.Id)?.ZoomAndCenterRectangle(min, max);
        }, cancellationToken);
    }

    public Task SectionBoxAsync(IReadOnlyCollection<ElementRef> elements, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Section box", application =>
        {
            var (active, view) = CurrentView.Require(application);
            if (view is not View3D view3D)
            {
                throw new InvalidOperationException(VisibilityText.Needs3DView);
            }

            if (!ViewFraming.TryFrame(active.Document, view, elements, BoxFactor, out var min, out var max))
            {
                throw new InvalidOperationException(VisibilityText.NothingToFrame);
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

    public Task<ActiveView?> ReadActiveViewAsync(CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync<ActiveView?>("Temporary state", application =>
        {
            if (application.ActiveUIDocument is not { ActiveView: { } view } active)
            {
                return null;
            }

            return new ActiveView(CurrentView.InfoOf(active.Document, view),
                view.IsTemporaryHideIsolateActive() || IsPropertiesModeActive(view));
        }, cancellationToken);
    }

    public async Task<IReadOnlyCollection<long>> ReadHiddenAsync(
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

    private HashSet<ElementId> IsolatedOutOf(Document model, View view)
    {
        return IsPropertiesModeActive(view) && _categoriesOff.TryGetValue(KeyOf(model, view), out var off) ? off.All : [];
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
               || (element.Category is { } category
                   && (isolatedOut.Contains(category.Id) || category.Parent is { } parent && isolatedOut.Contains(parent.Id)));
    }

    private Task<PlanOutcome> HideWithLinksAsync(VisibilityPlan plan, CancellationToken cancellationToken)
    {
        var apiSteps = plan.Steps.Where(step => step is not LinkElementsStep).ToList();
        var linked = plan.Steps.OfType<LinkElementsStep>()
            .SelectMany(step => step.Ids.Select(id => new ElementRef(step.Document, id)))
            .ToList();
        return RevitProfiler.RunAsync("Hide links", application =>
        {
            var (active, _) = CurrentView.Require(application, plan.ViewId);
            return InGroup(active.Document, HideName, () =>
            {
                var notice = apiSteps.Count > 0 ? Execute(application, plan with { Steps = apiSteps }) : null;
                return new PlanOutcome(LinkHideShow.Hide(application, linked), notice);
            });
        }, cancellationToken);
    }

    private Task<PlanOutcome> IsolateWithLinksAsync(VisibilityPlan plan, CancellationToken cancellationToken)
    {
        return RevitProfiler.RunAsync("Isolate links", application =>
        {
            var (active, view) = CurrentView.Require(application, plan.ViewId);
            if (view.IsInTemporaryViewMode(TemporaryViewMode.RevealHiddenElements))
            {
                throw new InvalidOperationException(VisibilityText.RevealOn);
            }

            var model = active.Document;
            var rest = LinkIsolation.RestOf(model, view, plan.Steps.OfType<LinkIsolateStep>());
            return InGroup(model, IsolateName, () =>
            {
                var notice = Execute(application, plan);
                return new PlanOutcome(rest.Count > 0 ? LinkHideShow.Hide(application, rest) : [], notice);
            });
        }, cancellationToken);
    }

    private static PlanOutcome InGroup(Document model, string name, Func<PlanOutcome> work)
    {
        RevitErrors.EnsureEditable(model);
        using var group = new TransactionGroup(model, name);
        group.Start();
        try
        {
            var outcome = work();
            group.Assimilate();
            return outcome;
        }
        catch
        {
            if (group.HasStarted())
            {
                group.RollBack();
            }

            throw;
        }
    }

    private string? Execute(UIApplication application, VisibilityPlan plan)
    {
        var (active, view) = CurrentView.Require(application, plan.ViewId);
        var model = active.Document;
        var (ids, categories) = CollectTargets(model, plan.Steps);
        var filters = LinkViewFilters.Prepare(model, view, plan.Steps.OfType<LinkFilterStep>());
        RevitErrors.EnsureEditable(model);
        using var transaction = new Transaction(model, TransactionName);
        transaction.Start();
        var notice = ApplyAction(model, view, plan, ids, categories, filters);
        transaction.Commit();
        return notice;
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
        IEnumerable<PlanStep> steps)
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
                case LinkInstanceStep link:
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
                case CategoryStep group:
                    categories.Add(group.CategoryId.ToElementId());
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

    private string? ApplyAction(
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
                Hide(model, view, ids, categories);
                break;
            case VisibilityAction.Isolate:
                Isolate(model, view, ids, categories);
                break;
            case VisibilityAction.Halftone:
            case VisibilityAction.Color:
                Override(model, view, plan, ids, categories, filters);
                break;
        }

        return categories.Count > 0 ? NoticeOf(model, view) : null;
    }

    private static void EnsureModel(DocumentKey document, string modelId)
    {
        if (!string.Equals(document.ModelId, modelId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(VisibilityText.ModelChanged);
        }
    }

    private static bool IsPropertiesModeActive(View view)
    {
        return view.TemporaryViewModes?.IsModeActive(TemporaryViewMode.TemporaryViewProperties) == true;
    }

    private void Hide(Document model, View view, List<ElementId> ids, List<ElementId> categories)
    {
        if (ids.Count > 0)
        {
            view.HideElementsTemporary(ids);
        }

        var hideable = categories.Where(view.CanCategoryBeHidden).ToList();
        if (hideable.Count == 0)
        {
            return;
        }

        EnsurePropertiesMode(view);
        var off = CategoriesOffOf(model, view);
        foreach (var category in hideable)
        {
            view.SetCategoryHidden(category, true);
            off.Hidden.Add(category);
        }
    }

    private void Isolate(Document model, View view, List<ElementId> ids, List<ElementId> categories)
    {
        if (ids.Count > 0)
        {
            view.IsolateElementsTemporary(TagHosts.WithHosts(model, ids));
        }

        if (categories.Count > 0)
        {
            EnsurePropertiesMode(view);
            var off = CategoriesOffOf(model, view);
            off.Isolated.Clear();
            off.Isolated.UnionWith(CategoryIsolation.Apply(model, view, categories, off.Hidden));
        }
    }

    private CategoriesOff CategoriesOffOf(Document model, View view)
    {
        var key = KeyOf(model, view);
        if (!_categoriesOff.TryGetValue(key, out var off))
        {
            off = new CategoriesOff();
            _categoriesOff.Add(key, off);
        }

        return off;
    }

    private sealed class CategoriesOff
    {
        public HashSet<ElementId> Hidden { get; } = [];

        public HashSet<ElementId> Isolated { get; } = [];

        public HashSet<ElementId> All => [.. Hidden, .. Isolated];
    }

    private static string? NoticeOf(Document model, View view)
    {
        var own = model.CollectElements()
            .OfClass<RevitLinkInstance>()
            .Cast<RevitLinkInstance>()
            .Count(instance => instance.GetLinkDocument() is not null
                               && !instance.IsHidden(view)
                               && RevitVisibilityExplainer.DisplayModeOf(view, instance) is not null);
        return own == 0 ? null : VisibilityText.LinksKeepOwnSettings;
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

        LinkViewFilters.Apply(model, view, filters, settings => Apply(settings, plan, solid));
    }

    private static void EnsurePropertiesMode(View view)
    {
        if (IsPropertiesModeActive(view))
        {
            return;
        }

        if (!view.CanEnableTemporaryViewPropertiesMode())
        {
            throw new InvalidOperationException(VisibilityText.NoTemporaryProperties);
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

}
