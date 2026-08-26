using AcadDevTool.Adapters;
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
    private static IHost? host;
    private static bool processTelemetryHandlersRegistered;

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

        host = builder.Build();
        RegisterProcessTelemetryHandlers();
        HostUiHelper.RunBlocking(() => host.StartAsync());
    }

    public static void Stop()
    {
        if (host is null)
        {
            return;
        }

        if (host.Services.GetService<ITelemetry>() is { } telemetry)
        {
            telemetry.Flush();
        }

        host.StopAsync().GetAwaiter().GetResult();
        host.Dispose();
        host = null;
    }

    public static T GetService<T>() where T : class
    {
        return host!.Services.GetRequiredService<T>();
    }

    public static object? GetService(Type serviceType)
    {
        return host!.Services.GetService(serviceType);
    }

    private static void RegisterProcessTelemetryHandlers()
    {
        if (processTelemetryHandlersRegistered)
        {
            return;
        }

        processTelemetryHandlersRegistered = true;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                if (args.ExceptionObject is not Exception ex || !TelemetryReporting.ShouldReportCriticalException(ex))
                {
                    return;
                }

                if (host?.Services.GetService<ITelemetry>() is not { } telemetry)
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
