using RevitDevTool.Tools.CommandBrowser.Views;
using RevitDevTool.Tools.Helpers;

namespace RevitDevTool.Tools.ElementFinder;

public partial class ElementFinderView
{
    public ElementFinderView()
    {
        InitializeComponent();
        ChromelessToolWindow.Attach(this);
    }
}
