using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;

namespace DevTools.ModelsContext.Visibility;

internal sealed class ViewLedger
{
    public HashSet<ElementRef> HiddenElements { get; } = [];

    public Dictionary<long, string> HiddenCategories { get; } = [];

    public HashSet<DocumentKey> HiddenLinks { get; } = [];

    public List<VisibilityPlan> OverridePlans { get; } = [];

    public Dictionary<ElementRef, RowOverride> Overrides { get; } = [];

    public Dictionary<long, RowOverride> CategoryOverrides { get; } = [];

    public Isolation? Isolation { get; set; }

    public Dictionary<DocumentKey, HashSet<long>> CategoryOffIds { get; } = [];

    public Dictionary<DocumentKey, HashSet<long>> PanelHiddenIds { get; } = [];

    public void AddPanelHiddenIds(DocumentKey document, IEnumerable<long> ids)
    {
        if (!PanelHiddenIds.TryGetValue(document, out var set))
        {
            set = [];
            PanelHiddenIds.Add(document, set);
        }

        set.UnionWith(ids);
    }

    public bool IsCategoryOff(long categoryId)
    {
        return HiddenCategories.ContainsKey(categoryId)
               || Isolation is { KeptCategories.Count: > 0 } isolation && !isolation.KeptCategories.Contains(categoryId);
    }

    public void AddCategoryOffIds(DocumentKey document, IEnumerable<long> ids)
    {
        if (!CategoryOffIds.TryGetValue(document, out var set))
        {
            set = [];
            CategoryOffIds.Add(document, set);
        }

        set.UnionWith(ids);
    }

    public bool IsEmpty => HiddenElements.Count == 0 && HiddenCategories.Count == 0 && HiddenLinks.Count == 0
                           && Isolation is null;

    public bool IsHidden(ElementRef element, long categoryId)
    {
        if (Isolation is { } isolation && !isolation.Keeps(element, categoryId))
        {
            return true;
        }

        return HiddenElements.Contains(element) || HiddenCategories.ContainsKey(categoryId)
                                                || HiddenLinks.Contains(element.Document);
    }

    public void Add(VisibilityPlan plan)
    {
        foreach (var step in plan.Steps)
        {
            switch (step)
            {
                case CategoryStep category:
                    HiddenCategories[category.CategoryId] = category.Name;
                    break;
                case LinkInstanceStep link:
                    HiddenLinks.Add(link.Document);
                    break;
                case ElementsStep elements:
                    HiddenElements.UnionWith(elements.Ids.Select(id => new ElementRef(elements.Document, id)));
                    break;
            }
        }
    }

    public void Remove(VisibilityPlan plan)
    {
        foreach (var step in plan.Steps)
        {
            switch (step)
            {
                case CategoryStep category:
                    HiddenCategories.Remove(category.CategoryId);
                    break;
                case LinkInstanceStep link:
                    HiddenLinks.Remove(link.Document);
                    break;
                case ElementsStep elements:
                    HiddenElements.ExceptWith(elements.Ids.Select(id => new ElementRef(elements.Document, id)));
                    break;
            }
        }
    }

    public void AddOverrides(VisibilityPlan plan)
    {
        foreach (var step in plan.Steps)
        {
            switch (step)
            {
                case CategoryStep category:
                    CategoryOverrides.TryGetValue(category.CategoryId, out var current);
                    CategoryOverrides[category.CategoryId] = current.With(plan);
                    break;
                case LinkInstanceStep link:
                    AddOverride(InstanceOf(link.Document), plan);
                    break;
                case ElementsStep elements:
                    AddOverrides(elements.Document, elements.Ids, plan);
                    break;
                case LinkFilterStep filter:
                    AddOverrides(filter.Document, filter.Ids, plan);
                    break;
            }
        }
    }

    public void Forget(DocumentKey link)
    {
        foreach (var element in Overrides.Keys.Where(element => element.Document == link).ToList())
        {
            Overrides.Remove(element);
        }

        Overrides.Remove(InstanceOf(link));
        HiddenLinks.Remove(link);
        CategoryOffIds.Remove(link);
        OverridePlans.RemoveAll(plan => plan.Steps.Any(step => step is DocumentStep document && document.Document == link));
        Isolation?.KeptLinks.Remove(link);
    }

    public void Keep()
    {
        HiddenElements.Clear();
        HiddenLinks.Clear();
        PanelHiddenIds.Clear();
        Isolation = Isolation is { KeptCategories.Count: > 0 } isolation
            ? new Isolation([], [.. isolation.KeptCategories], [])
            : null;
    }

    public void Rename(ModelRename rename)
    {
        var elements = HiddenElements.Select(rename.Map).ToList();
        HiddenElements.Clear();
        HiddenElements.UnionWith(elements);
        var links = HiddenLinks.Select(rename.Map).ToList();
        HiddenLinks.Clear();
        HiddenLinks.UnionWith(links);
        var off = CategoryOffIds.ToList();
        CategoryOffIds.Clear();
        foreach (var pair in off)
        {
            CategoryOffIds[rename.Map(pair.Key)] = pair.Value;
        }

        var panel = PanelHiddenIds.ToList();
        PanelHiddenIds.Clear();
        foreach (var pair in panel)
        {
            PanelHiddenIds[rename.Map(pair.Key)] = pair.Value;
        }

        var overrides = Overrides.ToList();
        Overrides.Clear();
        foreach (var pair in overrides)
        {
            Overrides[rename.Map(pair.Key)] = pair.Value;
        }

        for (var index = 0; index < OverridePlans.Count; index++)
        {
            OverridePlans[index] = OverridePlans[index].Renamed(rename);
        }

        if (Isolation is { } isolation)
        {
            Isolation = new Isolation([.. isolation.KeptElements.Select(rename.Map)], isolation.KeptCategories,
                [.. isolation.KeptLinks.Select(rename.Map)]);
        }
    }

    public bool DropLinkFilters()
    {
        var plans = OverridePlans.Where(plan => plan.Steps.Any(step => step is LinkFilterStep)).ToList();
        foreach (var step in plans.SelectMany(plan => plan.Steps).OfType<LinkFilterStep>())
        {
            foreach (var id in step.Ids)
            {
                Overrides.Remove(new ElementRef(step.Document, id));
            }
        }

        foreach (var plan in plans)
        {
            var index = OverridePlans.IndexOf(plan);
            var rest = plan.Steps.Where(step => step is not LinkFilterStep).ToList();
            if (rest.Count == 0)
            {
                OverridePlans.RemoveAt(index);
            }
            else
            {
                OverridePlans[index] = plan with { Steps = rest };
            }
        }

        return plans.Count > 0;
    }

    public VisibilityPlan? HidePlan()
    {
        var steps = new List<PlanStep>();
        steps.AddRange(HiddenCategories.Select(pair => new CategoryStep(pair.Key, pair.Value)));
        steps.AddRange(HiddenLinks.Select(link => new LinkInstanceStep(link)));
        foreach (var group in HiddenElements.Where(element => element.Document.IsCurrentModel)
                     .GroupBy(element => element.Document))
        {
            steps.Add(new ElementsStep(group.Key, group.Select(element => element.Id).OrderBy(id => id).ToList()));
        }

        return steps.Count == 0 ? null : new VisibilityPlan(VisibilityAction.Hide, null, steps, null);
    }

    private static ElementRef InstanceOf(DocumentKey link)
    {
        return new ElementRef(link.CurrentModel, link.LinkInstanceId);
    }

    private void AddOverrides(DocumentKey document, IEnumerable<long> ids, VisibilityPlan plan)
    {
        foreach (var id in ids)
        {
            AddOverride(new ElementRef(document, id), plan);
        }
    }

    private void AddOverride(ElementRef element, VisibilityPlan plan)
    {
        Overrides.TryGetValue(element, out var current);
        Overrides[element] = current.With(plan);
    }
}

internal sealed record Isolation(
    HashSet<ElementRef> KeptElements,
    HashSet<long> KeptCategories,
    HashSet<DocumentKey> KeptLinks)
{
    public static Isolation From(VisibilityPlan plan)
    {
        var isolation = new Isolation([], [], []);
        foreach (var step in plan.Steps)
        {
            switch (step)
            {
                case CategoryStep category:
                    isolation.KeptCategories.Add(category.CategoryId);
                    break;
                case LinkInstanceStep link:
                    isolation.KeptLinks.Add(link.Document);
                    break;
                case LinkIsolateStep isolate:
                    isolation.KeptLinks.Add(isolate.Document);
                    break;
                case ElementsStep elements:
                    isolation.KeptElements.UnionWith(elements.Ids.Select(id => new ElementRef(elements.Document, id)));
                    break;
            }
        }

        return isolation;
    }

    public bool Keeps(ElementRef element, long categoryId)
    {
        return KeptElements.Contains(element) || KeptCategories.Contains(categoryId) || KeptLinks.Contains(element.Document);
    }
}
