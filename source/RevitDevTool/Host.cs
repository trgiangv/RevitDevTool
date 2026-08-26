using Microsoft.Extensions.DependencyInjection;
using DevTools.Execution.Providers.Python;
using DevTools.Telemetry;
using DevTools.UI;
using DevTools.Utilities;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Core;
using RevitDevTool.Composition;
using RevitDevTool.Logging.Linkify;
using RevitDevTool.Settings;

namespace RevitDevTool;

/// <summary>
/// Application host bootstrap. Composition: SettingServices → LoggingServices → Telemetry → ApplicationServices → ExecutionServices.
/// </summary>
public static class Host
{
    private static IHost? host;
    private static bool processTelemetryHandlersRegistered;

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
               .AddDevToolsTelemetry(
                   sp => sp.GetRequiredService<IRevitSettingsService>().GeneralConfig.EnableTelemetry,
                   _ => BuiltInSentryDsn.Value)
               .AddApplicationServices()
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
