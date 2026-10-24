using DevTools.Execution.Providers.Python;
using DevTools.UI;
using DevTools.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Composition;
using RevitDevTool.Core;
using RevitDevTool.Logging.Linkify;
using RevitDevTool.Settings;

namespace RevitDevTool;

/// <summary>
/// Application host bootstrap. Composition: SettingServices → LoggingServices → ApplicationServices (incl. telemetry) → ExecutionServices.
/// </summary>
public static class Host
{
    private static IHost? _host;

    public static void Start()
    {
        SetupTheme();
        var contentRoot = AppUtils.GetContentRootPath(RevitContext.Application.VersionNumber);
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
               .AddLoggingServices(v => v
                   .WithLinkify(new RevitLinkifier())
                   .WithCustomSerializer(new PythonJsonSerializer()))
               .AddApplicationServices()
               .AddExecutionServices();

        _host = builder.Build();
        HostUiHelper.RequireCompleted(_host.StartAsync());
    }

    public static void Stop()
    {
        if (_host is null)
        {
            return;
        }

        _host.StopAsync().GetAwaiter().GetResult();
        _host.Dispose();
        _host = null;
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
        DevTools.UI.Theme.ThemeManager.Setup(
            () => UIThemeManager.CurrentTheme == UITheme.Dark
                ? DevTools.UI.Theme.AppTheme.Dark
                : DevTools.UI.Theme.AppTheme.Light,
            onChanged => UIFramework.ApplicationTheme.CurrentTheme.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(UIFramework.ApplicationTheme.CurrentTheme.RibbonPanelBackgroundBrush)) return;
                if (UIThemeManager.CurrentTheme.ToString() == UIFramework.ApplicationTheme.CurrentTheme.RibbonTheme.Name) return;
                HostUiHelper.RunOnMainThread(onChanged);
            });
#endif
    }
}
