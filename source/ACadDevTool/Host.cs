using AcadDevTool.HostAdapters;
using AcadDevTool.Settings;
using Microsoft.Extensions.DependencyInjection;
using DevTools.Execution.Providers.Python;
using DevTools.Telemetry;
using DevTools.UI;
using DevTools.UI.Theme;
using DevTools.Utilities;
using Microsoft.Extensions.Hosting;
using AcadDevTool.Composition;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace AcadDevTool;

/// <summary>
/// Application host bootstrap. Composition: SettingServices → LoggingServices → ApplicationServices → DevTools telemetry → ExecutionServices.
/// Registers <see cref="AppDomain.UnhandledException"/> for critical telemetry (same pattern as Revit).
/// </summary>
public static class Host
{
    private static IHost? _host;
    private static bool _processTelemetryHandlersRegistered;

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
               .AddDevToolsTelemetry(
                   sp => sp.GetRequiredService<IAcadSettingsService>().GeneralConfig.EnableTelemetry,
                   _ => BuiltInSentryDsn.Value)
               .AddExecutionServices();

        _host = builder.Build();
        RegisterProcessTelemetryHandlers();
        HostUiHelper.RunBlocking(() => _host.StartAsync());
    }

    public static void Stop()
    {
        if (_host is null)
        {
            return;
        }

        if (_host.Services.GetService<ITelemetry>() is { } telemetry)
        {
            telemetry.Flush();
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

    private static void RegisterProcessTelemetryHandlers()
    {
        if (_processTelemetryHandlersRegistered)
        {
            return;
        }

        _processTelemetryHandlersRegistered = true;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                if (args.ExceptionObject is not Exception ex || !TelemetryReporting.ShouldReportCriticalException(ex))
                {
                    return;
                }

                if (_host?.Services.GetService<ITelemetry>() is not { } telemetry)
                {
                    return;
                }

                telemetry.RecordCriticalException(ex, TelemetryKeys.Feature.AppDomain);
                telemetry.Flush();
            }
            catch
            {
                // Never throw from the unhandled handler.
            }
        };
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
