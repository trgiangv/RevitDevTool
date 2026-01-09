using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace RevitDevTool.Services;

public sealed class NavigationServiceFactory(INavigationViewPageProvider pageProvider) : INavigationServiceFactory
{
    public NavigationService Create(NavigationView navigationView)
    {
        var navigationService = new NavigationService(pageProvider);
        navigationService.SetNavigationControl(navigationView);
        return navigationService;
    }
}





