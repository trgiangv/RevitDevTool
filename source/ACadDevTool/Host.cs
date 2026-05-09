using AcadDevTool.HostAdapters;
using Microsoft.Extensions.DependencyInjection;
using DevTools.UI.Theme;
using DevTools.Utilities;
using Microsoft.Extensions.Hosting;
using AcadDevTool.Hosting;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace AcadDevTool;

/// <summary>
/// Application host bootstrap. Four composition groups: SettingServices → LoggingServices → ApplicationServices → ExecutionServices.
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
               .AddLoggingServices()
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
