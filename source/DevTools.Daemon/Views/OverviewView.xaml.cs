using System.Windows;
using System.Windows.Controls;
using DevTools.Daemon.Desktop;

namespace DevTools.Daemon.Views;

public partial class OverviewView : UserControl
{
    public OverviewView()
    {
        InitializeComponent();
    }

    private void OnSignInClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state)
            _ = state.SignIn();
    }
}
