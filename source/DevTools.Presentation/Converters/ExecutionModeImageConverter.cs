using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Visibility = System.Windows.Visibility;

namespace DevTools.Presentation.Converters;

/// <summary>
/// Converts <see cref="ExecutionMode"/> to the matching icon ImageSource.
/// Returns null when the value is null or unrecognised.
/// </summary>
[ValueConversion(typeof(ExecutionMode), typeof(ImageSource))]
public sealed class ExecutionModeImageConverter : IValueConverter
{
    private static readonly ResourceDictionary Icons = LoadIcons();

    private static ResourceDictionary LoadIcons()
    {
        var dict = new ResourceDictionary
        {
            Source = new Uri("/DevTools.UI;component/Theme/Styles/Icons.xaml", UriKind.Relative)
        };
        return dict;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ExecutionMode mode)
            return null;

        var key = mode switch
        {
            ExecutionMode.Python     => "PythonIcon",
            ExecutionMode.IronPython => "IronPythonIcon",
            ExecutionMode.FSharp     => "FsharpIcon",
            ExecutionMode.CSharp     => "CsharpIcon",
            ExecutionMode.Dotnet     => "DotnetIcon",
            _                        => "DotnetIcon",
        };
        return Icons[key];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts <see cref="ExecutionMode"/> (or nullable ExecutionMode) to Visibility.
/// Null / unrecognised → Collapsed; valid mode → Visible.
/// </summary>
[ValueConversion(typeof(ExecutionMode), typeof(Visibility))]
public sealed class ExecutionModeVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ExecutionMode ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
