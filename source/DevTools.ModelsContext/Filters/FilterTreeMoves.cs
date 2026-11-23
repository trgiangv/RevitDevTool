namespace DevTools.ModelsContext.Filters;

public static class FilterTreeMoves
{
    public static bool CanMove<T>(T item, T target, Func<T, T?> parentOf, Func<T, bool> isReadOnly)
        where T : class
    {
        return parentOf(item) is { } parent && !isReadOnly(parent) && !isReadOnly(target)
               && !IsWithin(target, item, parentOf);
    }

    public static bool IsWithin<T>(T node, T ancestor, Func<T, T?> parentOf)
        where T : class
    {
        for (T? current = node; current is not null; current = parentOf(current))
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    public static int? WithinGroup(int from, int index, int count)
    {
        var to = Math.Max(0, Math.Min(index > from ? index - 1 : index, count - 1));
        return to == from ? null : to;
    }

    public static int IntoGroup(int index, int count)
    {
        return Math.Max(0, Math.Min(index, count));
    }

    public static int Up(int from)
    {
        return from - 1;
    }

    public static int Down(int from)
    {
        return from + 2;
    }

    public static bool CanMoveUp(int index)
    {
        return index > 0;
    }

    public static bool CanMoveDown(int index, int count)
    {
        return index < count - 1;
    }
}
