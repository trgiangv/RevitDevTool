using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.Geometry;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class RevitViewActions : IViewActions
{
    public const string Needs3DView = "Section box needs a 3D view.";
    public const string NoTemporaryProperties = "This view cannot take temporary overrides.";
    public const string ModelChanged = "The active model changed.";
    public const string NothingToFrame = "Nothing to frame.";

    private const string TransactionName = "Model Context";
    private const double ZoomFactor = 1.25;
    private const double BoxFactor = 1.0;

    public Task ExecuteAsync(VisibilityPlan plan, CancellationToken cancellationToken)
    {
        if (plan.DisabledReason is { } reason)
        {
            throw new InvalidOperationException(reason);
        }

        if (!plan.UsesReferenceCommands)
        {
            return RevitProfiler.RunAsync("Visibility", application => Execute(application, plan), cancellationToken);
        }

        if (plan.Action != VisibilityAction.Hide)
        {
            throw new InvalidOperationException(VisibilityReasons.LinkChildOnlyHideShow);
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
            if (!hideIsolate && !properties)
            {
                return;
            }

            RevitErrors.EnsureEditable(active.Document);
            using var transaction = new Transaction(active.Document, TransactionName);
            transaction.Start();
            if (hideIsolate)
            {
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
            }

            if (properties)
            {
                view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryViewProperties);
            }

            transaction.Commit();
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

                return HiddenSlice(application.ActiveUIDocument.Document, view, ids, start);
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
            view.Id.ToString(), view.Name, view is View3D);
    }

    private static (int Next, List<long> Hidden) HiddenSlice(Document model, View view, long[] ids, int start)
    {
        var temporary = view.IsTemporaryHideIsolateActive();
        var hidden = new List<long>();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var index = start;
        while (index < ids.Length && index - start < ElementReader.SliceSize && watch.Elapsed < ElementReader.SliceTime)
        {
            var id = ids[index].ToElementId();
            if (model.GetElement(id) is { } element
                && (element.IsHidden(view)
                    || (temporary && !view.IsElementVisibleInTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate, id))))
            {
                hidden.Add(ids[index]);
            }

            index++;
        }

        return (index, hidden);
    }

    private static Task HideWithLinksAsync(VisibilityPlan plan, CancellationToken cancellationToken)
    {
        var apiSteps = plan.Steps.Where(step => step is not ReferenceCommandStep).ToList();
        var linked = plan.Steps.OfType<ReferenceCommandStep>()
            .SelectMany(step => step.Ids.Select(id => new ElementRef(step.Document, id)))
            .ToList();
        return RevitProfiler.RunAsync("Hide links", application =>
        {
            if (apiSteps.Count > 0)
            {
                Execute(application, plan with { Steps = apiSteps });
            }

            LinkedHideShow.Hide(application, linked);
        }, cancellationToken);
    }

    private static void Execute(UIApplication application, VisibilityPlan plan)
    {
        var (active, view) = RequireView(application);
        var model = active.Document;
        var (ids, categories) = CollectTargets(model, plan.Steps);
        RevitErrors.EnsureEditable(model);
        using var transaction = new Transaction(model, TransactionName);
        transaction.Start();
        ApplyAction(model, view, plan, ids, categories);
        transaction.Commit();
    }

    private static (List<ElementId> Ids, List<ElementId> Categories) CollectTargets(
        Document model,
        IEnumerable<VisibilityStep> steps)
    {
        var modelId = DocumentRegistry.ModelIdOf(model);
        var ids = new List<ElementId>();
        var categories = new List<ElementId>();
        foreach (var step in steps)
        {
            switch (step)
            {
                case ElementsStep elements:
                    EnsureModel(elements.Document, modelId);
                    ids.AddRange(elements.Ids.Select(id => id.ToElementId()).Where(id => model.GetElement(id) is not null));
                    break;
                case AttachedModelStep link:
                    EnsureModel(link.Document, modelId);
                    AddLinkInstance(model, link.Document, ids);
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

    private static void ApplyAction(
        Document model,
        View view,
        VisibilityPlan plan,
        List<ElementId> ids,
        List<ElementId> categories)
    {
        switch (plan.Action)
        {
            case VisibilityAction.Hide:
                Hide(view, ids, categories);
                break;
            case VisibilityAction.Isolate:
                Isolate(model, view, ids, categories);
                break;
            case VisibilityAction.Halftone:
            case VisibilityAction.Color:
                Override(model, view, plan, ids, categories);
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

    private static void Isolate(Document model, View view, List<ElementId> ids, List<ElementId> categories)
    {
        if (ids.Count == 0)
        {
            if (categories.Count > 0)
            {
                view.IsolateCategoriesTemporary(categories);
            }

            return;
        }

        var isolated = new List<ElementId>(ids);
        if (categories.Count > 0)
        {
            isolated.AddRange(new FilteredElementCollector(model, view.Id)
                .WherePasses(new ElementMulticategoryFilter(categories))
                .WhereElementIsNotElementType()
                .ToElementIds());
        }

        view.IsolateElementsTemporary(isolated);
    }

    private static void Override(
        Document model,
        View view,
        VisibilityPlan plan,
        List<ElementId> ids,
        List<ElementId> categories)
    {
        EnsurePropertiesMode(view);
        var solid = plan.Action == VisibilityAction.Color ? SolidFillOf(model) : null;
        foreach (var id in ids)
        {
            view.SetElementOverrides(id, Apply(view.GetElementOverrides(id), plan, solid));
        }

        foreach (var category in categories.Where(view.IsCategoryOverridable))
        {
            view.SetCategoryOverrides(category, Apply(view.GetCategoryOverrides(category), plan, solid));
        }
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
        return new FilteredElementCollector(model)
            .OfClass(typeof(FillPatternElement))
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
