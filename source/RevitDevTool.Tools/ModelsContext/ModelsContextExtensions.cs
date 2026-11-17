using DevTools.ModelsContext.Ports;
using DevTools.ModelsContext.ViewModels;
using DevTools.Presentation.Views.ModelsContext;
using DevTools.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows.Threading;

namespace RevitDevTool.Tools.ModelsContext;

public static class ModelsContextExtensions
{
    public static IServiceCollection AddModelsContext(this IServiceCollection services)
    {
        services.AddSingleton<ScopeCache>();
        services.AddSingleton<ParameterCache>();
        services.AddSingleton<RevitModelEvents>(sp => new RevitModelEvents(
            sp.GetRequiredService<ScopeCache>(),
            sp.GetRequiredService<ParameterCache>(),
            Dispatcher.CurrentDispatcher));
        services.AddSingleton<IRevitModelEvents>(sp => sp.GetRequiredService<RevitModelEvents>());
        services.AddSingleton<IModelEvents>(sp => sp.GetRequiredService<RevitModelEvents>());
        services.AddSingleton<IModelSource, RevitModelSource>();
        services.AddSingleton<ISelectionPort, RevitSelectionPort>();
        services.AddSingleton<IViewActions, RevitViewActions>();
        services.AddSingleton<IFilterEvaluator, RevitFilterEvaluator>();
        services.AddSingleton<IHostFilterCatalog, RevitFilterCatalog>();
        services.AddSingleton<ICustomFilterStore, CustomFilterStore>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IScheduler>(_ => new DispatcherScheduler(Dispatcher.CurrentDispatcher));
        services.AddSingleton(sp => new ModelContextServices(
            sp.GetRequiredService<IModelSource>(),
            sp.GetRequiredService<IModelEvents>(),
            sp.GetRequiredService<ISelectionPort>(),
            sp.GetRequiredService<IViewActions>(),
            sp.GetRequiredService<IFilterEvaluator>(),
            sp.GetRequiredService<IHostFilterCatalog>(),
            sp.GetRequiredService<ICustomFilterStore>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<IScheduler>(),
            sp.GetRequiredService<ILogger<ModelsContextPane>>()));
        services.AddSingleton<ModelsContextPane>();
        return services;
    }
}
