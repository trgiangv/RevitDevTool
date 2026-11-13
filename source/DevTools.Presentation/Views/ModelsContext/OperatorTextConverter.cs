using DevTools.ModelsContext.Model;
using System.Globalization;
using System.Windows.Data;

namespace DevTools.Presentation.Views.ModelsContext;

public sealed class OperatorTextConverter : IValueConverter
{
    private static readonly Dictionary<FilterOperator, string> Names = new()
    {
        [FilterOperator.Equal] = "equals",
        [FilterOperator.NotEqual] = "does not equal",
        [FilterOperator.Greater] = "is greater than",
        [FilterOperator.GreaterOrEqual] = "is greater than or equal to",
        [FilterOperator.Less] = "is less than",
        [FilterOperator.LessOrEqual] = "is less than or equal to",
        [FilterOperator.Contains] = "contains",
        [FilterOperator.NotContains] = "does not contain",
        [FilterOperator.BeginsWith] = "begins with",
        [FilterOperator.NotBeginsWith] = "does not begin with",
        [FilterOperator.EndsWith] = "ends with",
        [FilterOperator.NotEndsWith] = "does not end with",
        [FilterOperator.HasValue] = "has a value",
        [FilterOperator.HasNoValue] = "has no value",
        [FilterOperator.Exists] = "parameter exists",
        [FilterOperator.Missing] = "parameter missing"
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is FilterOperator op && Names.TryGetValue(op, out var name) ? name : value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
