using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace DevTools.ModelsContext.Model;

public static class EnumText
{
    private static readonly ConcurrentDictionary<Enum, string> Cache = new();

    public static string Of(Enum value)
    {
        return Cache.GetOrAdd(value, static key =>
        {
            var name = key.ToString();
            var member = key.GetType().GetField(name, BindingFlags.Public | BindingFlags.Static);
            return member?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? name;
        });
    }
}
