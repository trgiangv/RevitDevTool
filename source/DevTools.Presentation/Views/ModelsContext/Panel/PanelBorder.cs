using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace DevTools.Presentation.Views.ModelsContext.Panel;

public sealed class PanelBorder : Border
{
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new FrameworkElementAutomationPeer(this);
    }
}
