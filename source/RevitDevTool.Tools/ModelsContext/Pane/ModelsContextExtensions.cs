using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Selection;
using DevTools.ModelsContext.Visibility;
using DevTools.Presentation.Views.ModelsContext.Panel;
using DevTools.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Filters;
using RevitDevTool.Tools.ModelsContext.Parameters;
using RevitDevTool.Tools.ModelsContext.Selection;
using RevitDevTool.Tools.ModelsContext.Visibility;
using System.Windows.Threading;

namespace RevitDevTool.Tools.ModelsContext.Pane;

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
        services.AddSingleton<IVisibilityExplainer, RevitVisibilityExplainer>();
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
            sp.GetRequiredService<IVisibilityExplainer>(),
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
