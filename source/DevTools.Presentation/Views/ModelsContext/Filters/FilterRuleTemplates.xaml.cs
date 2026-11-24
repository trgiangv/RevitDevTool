using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace DevTools.Presentation.Views.ModelsContext.Filters;

public partial class FilterRuleTemplates : ResourceDictionary
{
    public FilterRuleTemplates()
    {
        InitializeComponent();
    }

    private void OnValuesOpened(object? sender, EventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RuleViewModel rule })
        {
            _ = rule.LoadValuesCommand.ExecuteAsync(null);
        }
    }

    private void OnMoreValuesClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Parent: FrameworkElement { Parent: Grid card } })
        {
            return;
        }

        var box = card.Children.OfType<Grid>()
            .SelectMany(row => row.Children.OfType<ComboBox>())
            .FirstOrDefault(combo => combo.IsVisible && AutomationProperties.GetAutomationId(combo) == "Filter.Rule.Value");
        if (box is not null)
        {
            box.Focus();
            box.IsDropDownOpen = true;
        }
    }

    private void OnValuesLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RuleViewModel { Kind: ValueKind.Reference or ValueKind.Enumerated } rule })
        {
            _ = rule.LoadValuesCommand.ExecuteAsync(null);
        }
    }
}
