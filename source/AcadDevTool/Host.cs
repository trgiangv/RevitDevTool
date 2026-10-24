using AcadDevTool.Adapters;
using AcadDevTool.Composition;
using AcadDevTool.Settings;
using DevTools.Execution.Providers.Python;
using DevTools.UI;
using DevTools.UI.Theme;
using DevTools.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace AcadDevTool;

/// <summary>
/// Application host bootstrap. Composition: SettingServices → LoggingServices → ApplicationServices (incl. telemetry) → ExecutionServices.
/// </summary>
public static class Host
{
    private static IHost? _host;

    public static void Start()
    {
        SetupTheme();
        var contentRoot = AppUtils.GetContentRootPath(AcadProductDetector.GetVersionNumber());
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
               .AddLoggingServices(v => v.WithCustomSerializer(new PythonJsonSerializer()))
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
        const string colorTheme = "COLORTHEME";
        ThemeManager.Setup(
            () => (short)AcadApp.GetSystemVariable(colorTheme) == 0 ? AppTheme.Dark : AppTheme.Light,
            onChanged => AcadApp.SystemVariableChanged += (_, e) =>
            {
                if (!string.Equals(e.Name, colorTheme, StringComparison.OrdinalIgnoreCase)) return;
                HostUiHelper.RunOnMainThread(onChanged);
            });
    }
}
