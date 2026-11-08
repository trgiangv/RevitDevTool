using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
// ReSharper disable once CheckNamespace
namespace DevTools.UI.Controls;

internal sealed class ColorEyeDropperAutomationPeer : ButtonAutomationPeer, IInvokeProvider
{
    public ColorEyeDropperAutomationPeer(ColorEyeDropper owner)
        : base(owner)
    {
    }

    void IInvokeProvider.Invoke()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }

        var dropper = (ColorEyeDropper)Owner;
        dropper.Pick(Mouse.GetPosition(dropper));
    }
}
