using Wpf.Ui;
using Wpf.Ui.Controls;

namespace RevitDevTool.Services;

public interface INavigationServiceFactory
{
    NavigationService Create(NavigationView navigationView);
}





