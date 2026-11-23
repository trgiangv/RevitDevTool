namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal static class CategoryIsolation
{
    public static HashSet<ElementId> Apply(Document model, View view, IReadOnlyCollection<ElementId> kept)
    {
        var keep = KeepOf(model, kept);
        var hidden = new HashSet<ElementId>();
        foreach (Category category in model.Settings.Categories)
        {
            if (!view.CanCategoryBeHidden(category.Id))
            {
                continue;
            }

            var shown = keep.Contains(category.Id);
            if (view.GetCategoryHidden(category.Id) != !shown)
            {
                view.SetCategoryHidden(category.Id, !shown);
            }

            if (!shown)
            {
                hidden.Add(category.Id);
            }
        }

        return hidden;
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
