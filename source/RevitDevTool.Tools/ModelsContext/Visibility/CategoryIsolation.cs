using RevitDevTool.Tools.ModelsContext.Documents;

namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal static class CategoryIsolation
{
    public static HashSet<ElementId> Apply(
        Document model,
        View view,
        IReadOnlyCollection<ElementId> checkedCategories,
        HashSet<ElementId> stayHidden)
    {
        var kept = new HashSet<ElementId>(checkedCategories);
        kept.UnionWith(TagHosts.HostCategoriesIn(model, view, [.. checkedCategories]));
        var keep = KeepOf(model, kept);
        var hidden = new HashSet<ElementId>();
        foreach (Category category in model.Settings.Categories)
        {
            SetShown(view, category.Id, keep.Contains(category.Id) && !stayHidden.Contains(category.Id), hidden);
        }

        foreach (var id in ElementReader.OwnedCategoryIds(model))
        {
            if (Category.GetCategory(model, id)?.Parent is { } parent && keep.Contains(parent.Id))
            {
                var shown = keep.Contains(id) || kept.Contains(parent.Id);
                SetShown(view, id, shown && !stayHidden.Contains(id), hidden);
            }
        }

        return hidden;
    }

    private static void SetShown(View view, ElementId category, bool shown, HashSet<ElementId> hidden)
    {
        if (!view.CanCategoryBeHidden(category))
        {
            return;
        }

        if (view.GetCategoryHidden(category) != !shown)
        {
            view.SetCategoryHidden(category, !shown);
        }

        if (!shown)
        {
            hidden.Add(category);
        }
    }

    private static HashSet<ElementId> KeepOf(Document model, IEnumerable<ElementId> kept)
    {
        var keep = new HashSet<ElementId> { new(BuiltInCategory.OST_RvtLinks) };
        foreach (var id in kept)
        {
            keep.Add(id);
            if (Category.GetCategory(model, id)?.Parent is { } parent)
            {
                keep.Add(parent.Id);
            }
        }

        return keep;
    }
}
