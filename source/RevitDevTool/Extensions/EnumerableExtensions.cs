namespace RevitDevTool.Extensions;

[PublicAPI]
public static class EnumerableExtensions
{
    public static void Dispose<T>(this IEnumerable<T?>? items) where T : IDisposable
    {
        if (items is null) return;
        foreach (var item in items)
        {
            item?.Dispose();
        }
    }
    
    public static void Dispose<T>(this T?[]? items) where T : IDisposable
    {
        if (items is null) return;
        foreach (var item in items)
        {
            item?.Dispose();
        }
    }
    
    public static void Clear<T>(this ICollection<T>? items, bool dispose = false) where T : IDisposable
    {
        if (items is null) return;

        if (dispose)
        {
            items.Dispose();
        }

        items.Clear();
    }
    
    public static void Clear<T>(this ICollection<T[]> items, bool dispose = false) where T : IDisposable
    {
        if (dispose)
        {
            foreach (var item in items)
            {
                item.Dispose();
            }
        }
        items.Clear();
    }
}