using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;
namespace DevTools.UI.Converters;

[ValueConversion(typeof(Enum), typeof(string))]
public class EnumDescriptionConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<Enum, string> Cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Enum member ? Cache.GetOrAdd(member, Describe) : value;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }

    private static string Describe(Enum member)
    {
        var name = member.ToString();
        var field = member.GetType().GetField(name, BindingFlags.Public | BindingFlags.Static);
        return field?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? name;
    }
}
