namespace DevTools.ModelsContext.Model;

public static class CustomFilterLibrary
{
    public static List<CustomFilter> Upsert(IReadOnlyList<CustomFilter> stored, CustomFilter? previous, CustomFilter filter)
    {
        var filters = stored.ToList();
        var index = previous is null ? -1 : filters.FindIndex(other => SameName(other.Name, previous.Name));
        if (index >= 0)
        {
            filters[index] = filter;
        }
        else
        {
            filters.Add(filter);
        }

        return filters;
    }

    public static List<CustomFilter> Remove(IReadOnlyList<CustomFilter> stored, string name)
    {
        return stored.Where(filter => !SameName(filter.Name, name)).ToList();
    }

    public static FilterDefinition Strip(FilterDefinition definition)
    {
        return new FilterDefinition(definition.Groups, Strip(definition.Root));
    }

    private static FilterSet Strip(FilterSet set)
    {
        return set with
        {
            Children = set.Children
                .Where(child => child is not UnsupportedRule)
                .Select(child => child is FilterSet nested ? Strip(nested) : child)
                .ToList()
        };
    }

    private static bool SameName(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
