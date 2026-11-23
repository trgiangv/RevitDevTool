namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal static class NestedComponents
{
    public static void AddWithNested(Document model, IEnumerable<ElementId> ids, List<ElementId> targets, HashSet<ElementId> seen)
    {
        var pending = new Stack<(ElementId Id, bool Nested)>(ids.Select(id => (id, false)));
        while (pending.Count > 0)
        {
            var (id, nested) = pending.Pop();
            if (!seen.Add(id))
            {
                continue;
            }

            var element = model.GetElement(id);
            if (element is null || (nested && element is not FamilyInstance))
            {
                continue;
            }

            targets.Add(id);
            if (element is FamilyInstance instance)
            {
                foreach (var sub in instance.GetSubComponentIds())
                {
                    pending.Push((sub, true));
                }
            }
        }
    }
}
