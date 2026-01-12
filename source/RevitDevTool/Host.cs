using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Controllers;
using RevitDevTool.Logging;
using RevitDevTool.Logging.ZLogger;
using RevitDevTool.Services;
using RevitDevTool.View;
using RevitDevTool.View.Settings;
using RevitDevTool.View.Settings.Visualization;
using RevitDevTool.ViewModel;
using RevitDevTool.ViewModel.Settings;
using RevitDevTool.ViewModel.Settings.Visualization;
using System.IO;
using System.Reflection;

namespace RevitDevTool;

public static class Host
{
    private static IHost? _host;

    public static void Start()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            DisableDefaults = true,
#if RELEASE
            EnvironmentName = Environments.Production
#else
            EnvironmentName = Environments.Development
#endif
        });

        ConfigureServices(builder.Services);

        _host = builder.Build();
        _host.Start();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddHostedService<HostBackgroundController>();

        // Serilog
        // services.AddSingleton<ILoggerFactory, SerilogLoggerFactory>();
        // services.AddSingleton<ITraceListenerFactory, SerilogTraceListenerFactory>();
        // services.AddSingleton<ILogOutputSink, SerilogRichTextBoxSink>();

        // ZLogger
        services.AddSingleton<ILoggerFactory, ZLoggerLoggerFactory>();
        services.AddSingleton<ITraceListenerFactory, ZLoggerTraceListenerFactory>();
        services.AddSingleton<ILogOutputSink, ZloggerRichTextBoxSink>();

        // Logging service
        services.AddSingleton<ILoggingService, LoggingService>();

        // Visualization
        services.AddSingleton<BoundingBoxVisualizationViewModel>();
        services.AddSingleton<FaceVisualizationViewModel>();
        services.AddSingleton<MeshVisualizationViewModel>();
        services.AddSingleton<PolylineVisualizationViewModel>();
        services.AddSingleton<SolidVisualizationViewModel>();
        services.AddSingleton<XyzVisualizationViewModel>();

        services.AddSingleton<BoundingBoxVisualizationSettingsView>();
        services.AddSingleton<FaceVisualizationSettingsView>();
        services.AddSingleton<MeshVisualizationSettingsView>();
        services.AddSingleton<PolylineVisualizationSettingsView>();
        services.AddSingleton<SolidVisualizationSettingsView>();
        services.AddSingleton<XyzVisualizationSettingsView>();

        // Settings
        services.AddSingleton<GeneralSettingsViewModel>();
        services.AddSingleton<GeneralSettingsView>();
        services.AddSingleton<LogSettingsViewModel>();

        // Root
        services.AddSingleton<TraceLogViewModel>();
        services.AddSingleton<TraceLogPageViewModel>();
        services.AddTransient<TraceLogPage>();
        services.AddTransient<TraceLogWindow>();
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
}
