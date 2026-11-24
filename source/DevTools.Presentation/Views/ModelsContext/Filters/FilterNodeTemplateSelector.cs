using DevTools.ModelsContext.Filters;
using System.Windows;
using System.Windows.Controls;

namespace DevTools.Presentation.Views.ModelsContext.Filters;

public sealed class FilterNodeTemplateSelector : DataTemplateSelector
{
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        var key = item switch
        {
            RuleSetViewModel => "RuleSetTemplate",
            RuleViewModel => "RuleTemplate",
            UnsupportedRuleViewModel => "UnsupportedRuleTemplate",
            _ => null
        };

        return key is not null && container is FrameworkElement element
            ? element.TryFindResource(key) as DataTemplate
            : null;
    }
}
