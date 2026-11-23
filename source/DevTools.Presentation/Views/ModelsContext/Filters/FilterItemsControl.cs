using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace DevTools.Presentation.Views.ModelsContext.Filters;

public sealed class FilterItemsControl : ItemsControl
{
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new FilterItemsPeer(this);
    }

    private sealed class FilterItemsPeer(FilterItemsControl owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Group;
        }

        protected override string GetClassNameCore()
        {
            return nameof(FilterItemsControl);
        }
    }
}
