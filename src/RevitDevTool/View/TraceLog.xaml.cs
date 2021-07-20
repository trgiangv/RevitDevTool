using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Autodesk.Revit.UI;

namespace RevitDevTool.View
{
    /// <summary>
    /// TraceLog.xaml 的交互逻辑
    /// </summary>
    public partial class TraceLog : Page, IDockablePaneProvider
    {
        public TraceLog()
        {
            InitializeComponent();
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = this;

            data.InitialState = new DockablePaneState()
            {
                DockPosition = DockPosition.Right,
                //TabBehind=DockablePanes.BuiltInDockablePanes.ProjectBrowser
            };

            data.FrameworkElement.MinWidth = 320;
        }
    }
}
