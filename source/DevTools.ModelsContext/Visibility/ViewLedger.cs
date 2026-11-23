using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;

namespace DevTools.ModelsContext.Visibility;

internal sealed class ViewLedger
{
    public HashSet<ElementRef> Elements { get; } = [];

    public Dictionary<long, string> Groups { get; } = [];

    public HashSet<DocumentKey> Links { get; } = [];

    public List<VisibilityPlan> Appearance { get; } = [];

    public Dictionary<ElementRef, RowOverride> Marks { get; } = [];

    public Dictionary<long, RowOverride> Categories { get; } = [];

    public LedgerIsolation? Isolation { get; set; }

    public bool IsEmpty => Elements.Count == 0 && Groups.Count == 0 && Links.Count == 0 && Isolation is null;

    public bool IsHidden(ElementRef element, long groupId)
    {
        if (Isolation is { } isolation && !isolation.Keeps(element, groupId))
        {
            return true;
        }

        return Elements.Contains(element) || Groups.ContainsKey(groupId) || Links.Contains(element.Document);
    }

    public void Add(VisibilityPlan plan)
    {
        foreach (var step in plan.Steps)
        {
            switch (step)
            {
                case GroupEverywhereStep group:
                    Groups[group.GroupId] = group.GroupName;
                    break;
                case AttachedModelStep model:
                    Links.Add(model.Document);
                    break;
                case ElementsStep elements:
                    Elements.UnionWith(elements.Ids.Select(id => new ElementRef(elements.Document, id)));
                    break;
                case ReferenceCommandStep references:
                    Elements.UnionWith(references.Ids.Select(id => new ElementRef(references.Document, id)));
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
                case GroupEverywhereStep group:
                    Groups.Remove(group.GroupId);
                    break;
                case AttachedModelStep model:
                    Links.Remove(model.Document);
                    break;
                case ElementsStep elements:
                    Elements.ExceptWith(elements.Ids.Select(id => new ElementRef(elements.Document, id)));
                    break;
            }
        }
    }

    public void Mark(VisibilityPlan plan)
    {
        foreach (var step in plan.Steps)
        {
            switch (step)
            {
                case GroupEverywhereStep group:
                    Categories.TryGetValue(group.GroupId, out var category);
                    Categories[group.GroupId] = category.With(plan);
                    break;
                case AttachedModelStep model:
                    MarkElement(InstanceOf(model.Document), plan);
                    break;
                case ElementsStep elements:
                    MarkElements(elements.Document, elements.Ids, plan);
                    break;
                case LinkFilterStep filter:
                    MarkElements(filter.Document, filter.Ids, plan);
                    break;
            }
        }
    }

    public void Forget(DocumentKey link)
    {
        foreach (var element in Marks.Keys.Where(element => element.Document == link).ToList())
        {
            Marks.Remove(element);
        }

        Marks.Remove(InstanceOf(link));
        Links.Remove(link);
        Elements.RemoveWhere(element => element.Document == link);
        Appearance.RemoveAll(plan => plan.Steps.Any(step => DocumentOf(step) == link));
        if (Isolation is { } isolation)
        {
            isolation.Links.Remove(link);
            isolation.Rest.RemoveWhere(element => element.Document == link);
        }
    }

    private static DocumentKey? DocumentOf(VisibilityStep step)
    {
        return step switch
        {
            AttachedModelStep model => model.Document,
            LinkFilterStep filter => filter.Document,
            LinkIsolateStep isolate => isolate.Document,
            _ => null
        };
    }

    public VisibilityPlan? HidePlan()
    {
        var steps = new List<VisibilityStep>();
        steps.AddRange(Groups.Select(pair => new GroupEverywhereStep(pair.Key, pair.Value)));
        steps.AddRange(Links.Select(link => new AttachedModelStep(link)));
        foreach (var group in Elements.Where(element => element.Document.IsCurrentModel)
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

    private void MarkElements(DocumentKey document, IEnumerable<long> ids, VisibilityPlan plan)
    {
        foreach (var id in ids)
        {
            MarkElement(new ElementRef(document, id), plan);
        }
    }

    private void MarkElement(ElementRef element, VisibilityPlan plan)
    {
        Marks.TryGetValue(element, out var mark);
        Marks[element] = mark.With(plan);
    }
}

internal sealed record LedgerIsolation(
    HashSet<ElementRef> Elements,
    HashSet<long> Groups,
    HashSet<DocumentKey> Links,
    HashSet<ElementRef> Rest)
{
    public static LedgerIsolation From(VisibilityPlan plan, IEnumerable<ElementRef> rest)
    {
        var isolation = new LedgerIsolation([], [], [], [.. rest]);
        foreach (var step in plan.Steps)
        {
            switch (step)
            {
                case GroupEverywhereStep group:
                    isolation.Groups.Add(group.GroupId);
                    break;
                case AttachedModelStep model:
                    isolation.Links.Add(model.Document);
                    break;
                case LinkIsolateStep link:
                    isolation.Links.Add(link.Document);
                    break;
                case ElementsStep elements:
                    isolation.Elements.UnionWith(elements.Ids.Select(id => new ElementRef(elements.Document, id)));
                    break;
            }
        }

        return isolation;
    }

    public bool Keeps(ElementRef element, long groupId)
    {
        return Elements.Contains(element) || Groups.Contains(groupId) || Links.Contains(element.Document);
    }
}
