using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Abstractions;

namespace RevitDevTool.Services;

internal sealed class PageProvider : INavigationViewPageProvider
{
    public object? GetPage(Type? pageType)
    {
        if (pageType == null) return null;

        // DI-only (like RevitLookup). Navigation pages should be registered in Host.
        return Host.GetService(pageType);
    }
}
