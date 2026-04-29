using Microsoft.Extensions.DependencyInjection;
using DevTools.UI.Theme;
using DevTools.Utilities;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Hosting;
using RevitDevTool.Logging.Linkify;

namespace RevitDevTool;

/// <summary>
/// Application host bootstrap. Four composition groups: SettingServices → LoggingServices → ApplicationServices → ExecutionServices.
/// </summary>
public static class Host
{
    private static IHost? _host;

    public static void Start()
    {
        SetupTheme();
        var contentRoot = AppUtils.GetContentRootPath();
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = contentRoot,
            DisableDefaults = true,
#if RELEASE
            EnvironmentName = Environments.Production
#else
            EnvironmentName = Environments.Development
#endif
        });

        builder.AddSettingServices(contentRoot)
               .AddLoggingServices(v => v.WithLinkify(new RevitLinkifier()))
               .AddApplicationServices()
               .AddExecutionServices();

        _host = builder.Build();
        _host.Start();
    }

    public static void Stop()
    {
        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();
    }

    public static T GetService<T>() where T : class
    {
        return _host!.Services.GetRequiredService<T>();
    }

    public static object? GetService(Type serviceType)
    {
        return _host!.Services.GetService(serviceType);
    }

    private static void SetupTheme()
    {
#if REVIT2024_OR_GREATER
        ThemeManager.Setup(
            () => UIThemeManager.CurrentTheme == UITheme.Dark
                ? AppTheme.Dark
                : AppTheme.Light,
            onChanged => UIFramework.ApplicationTheme.CurrentTheme.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(UIFramework.ApplicationTheme.CurrentTheme.RibbonPanelBackgroundBrush)) return;
                if (UIThemeManager.CurrentTheme.ToString() == UIFramework.ApplicationTheme.CurrentTheme.RibbonTheme.Name) return;
                DispatcherHelper.RunOnMainThread(onChanged);
            });
#endif
    }
}
