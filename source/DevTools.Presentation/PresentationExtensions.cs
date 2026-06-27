using DevTools.Presentation.ViewModels;
using DevTools.Presentation.ViewModels.Settings;
using DevTools.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevTools.Presentation;

/// <summary>
/// Shared shell UI registration for DevTools add-in hosts (views + view models + MCP tooling).
/// Hosts register messenger / main shell in the add-in, then call this overload.
/// </summary>
public static class PresentationExtensions
{
    public static IServiceCollection AddPresentationServices(this IServiceCollection services)
    {
        services.AddSingleton<CommandView>();
        services.AddSingleton<PackageView>();
        services.AddSingleton<MemoryView>();
        services.AddSingleton<ExecutionView>();
        services.AddSingleton<McpRegistryView>();
        services.TryAddSingleton<McpToolsetContextManager>();
        services.TryAddSingleton<DotnetMethodResolver>();

        services.AddTransient<SettingsViewModel>();
        services.AddTransient<GeneralSettingsViewModel>();
        services.AddSingleton<LogSettingsViewModel>();
        services.AddSingleton<McpSettingViewModel>();
        services.AddSingleton<LogViewModel>();
        services.AddSingleton<CommandViewModel>();
        services.AddSingleton<PackageViewModel>();
        services.AddSingleton<MemoryViewModel>();
        services.AddSingleton<ExecutionViewModel>();
        services.AddSingleton<McpRegistryViewModel>();
        services.AddSingleton<MainViewModel>();

        return services;
    }
}
