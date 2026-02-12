using Python.Runtime;
namespace RevitDevTool.Utils;

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

    public static List<object> AsObjectCollection(this PyObject pyObject)
    {
        if (!pyObject.IsIterable()) throw new ArgumentException("PyObject is not iterable", nameof(pyObject));
        var netList = new List<object>();
        using (Py.GIL())
        {
            dynamic pyList = pyObject;
            foreach (dynamic item in pyList)
            {
                var managedItem = item.AsManagedObject(typeof(object));
                if (managedItem is null) continue;
                switch (managedItem)
                {
                    case PyObject nestedPyObj when nestedPyObj.IsIterable():
                        netList.AddRange(nestedPyObj.AsObjectCollection());
                        break;
                    case IEnumerable<object> enumerable when managedItem is not string:
                        netList.AddRange(enumerable);
                        break;
                    default:
                        netList.Add(managedItem);
                        break;
                }
            }
        }
        return netList;
    }
}