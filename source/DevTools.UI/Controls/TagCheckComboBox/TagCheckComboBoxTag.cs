using HandyControl.Controls;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace DevTools.UI.Controls;

public sealed class TagCheckComboBoxTag : Tag
{
    private const string ElementClose = "ButtonClose";

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild(ElementClose) is UIElement close)
        {
            AutomationProperties.SetAutomationId(close, AutomationProperties.GetAutomationId(this) + "Remove");
            AutomationProperties.SetName(close, "Remove " + AutomationProperties.GetName(this));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TagAutomationPeer(this);

    private sealed class TagAutomationPeer(TagCheckComboBoxTag owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string GetClassNameCore() => nameof(TagCheckComboBoxTag);

        protected override string GetNameCore()
        {
            var name = base.GetNameCore();
            return string.IsNullOrEmpty(name) ? ((TagCheckComboBoxTag)Owner).Content?.ToString() ?? string.Empty : name;
        }
    }
}
