using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RevitDevTool.Scintilla.Control;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Render;
namespace RevitDevTool.Scintilla.Extensions;

public static class ScintillaServiceCollectionExtensions
{
    public static IServiceCollection AddScintillaLogViewerWinForms(this IServiceCollection services)
        => AddScintillaLogViewerWinForms(services, optionsFactory: null, renderStrategyFactory: null);

    public static IServiceCollection AddScintillaLogViewerWinForms(
        this IServiceCollection services,
        Func<IServiceProvider, ScintillaLogViewerOptions>? optionsFactory,
        Func<IServiceProvider, ILogRenderStrategy>? renderStrategyFactory = null)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        services.TryAddSingleton<ILogViewerControlEvents, LogViewerControlEvents>();
        services.AddSingleton(sp => new ScintillaLogViewer(optionsFactory?.Invoke(sp), renderStrategyFactory?.Invoke(sp)));
        services.AddSingleton<IScintillaLogViewHost>(sp => sp.GetRequiredService<ScintillaLogViewer>());
        return services;
    }

    public static IServiceCollection AddScintillaLogViewerWpf(this IServiceCollection services)
        => AddScintillaLogViewerWpf(services, optionsFactory: null, renderStrategyFactory: null);

    public static IServiceCollection AddScintillaLogViewerWpf(
        this IServiceCollection services,
        Func<IServiceProvider, ScintillaLogViewerOptions>? optionsFactory,
        Func<IServiceProvider, ILogRenderStrategy>? renderStrategyFactory = null)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));

        services.TryAddSingleton<ILogViewerControlEvents, LogViewerControlEvents>();
        services.AddSingleton(sp => new ScintillaLogViewerWpf(optionsFactory?.Invoke(sp), renderStrategyFactory?.Invoke(sp)));
        services.AddSingleton<IScintillaLogViewHost>(sp => sp.GetRequiredService<ScintillaLogViewerWpf>());
        return services;
    }
}
