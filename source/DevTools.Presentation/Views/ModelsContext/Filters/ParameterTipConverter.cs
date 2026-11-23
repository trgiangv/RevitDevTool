using DevTools.ModelsContext.Parameters;
using System.Globalization;
using System.Windows.Data;

namespace DevTools.Presentation.Views.ModelsContext.Filters;

public sealed class ParameterTipConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is ParameterInfo info ? ParameterTip.Of(info) : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
