using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.ViewModels;
using System.Windows;

namespace DevTools.Presentation.Views.ModelsContext;

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

    private void OnValuesLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: RuleViewModel { Kind: ValueKind.Reference or ValueKind.Enumerated } rule })
        {
            _ = rule.LoadValuesCommand.ExecuteAsync(null);
        }
    }
}
