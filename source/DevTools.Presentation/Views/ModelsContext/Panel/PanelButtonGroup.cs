using HandyControl.Controls;
using System.Windows.Automation.Peers;

namespace DevTools.Presentation.Views.ModelsContext.Panel;

public sealed class PanelButtonGroup : ButtonGroup
{
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new FrameworkElementAutomationPeer(this);
    }
}
