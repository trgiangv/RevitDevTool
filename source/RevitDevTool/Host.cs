using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Controllers;
using RevitDevTool.Logging;
using RevitDevTool.Logging.Serilog;
using RevitDevTool.Logging.ZLogger;
using RevitDevTool.Services;
using RevitDevTool.Services.Configuration;
using RevitDevTool.Utils;
using RevitDevTool.View;
using RevitDevTool.View.Settings;
using RevitDevTool.View.Settings.Visualization;
using RevitDevTool.ViewModel;
using RevitDevTool.ViewModel.Settings;
using RevitDevTool.ViewModel.Settings.Visualization;
using RevitDevTool.Visualization.Server;
using System.IO;

namespace RevitDevTool;

public static class Host
{
    private static IHost? _host;

    public static void Start()
    {
        var contentRoot = SettingsUtils.GetContentRootPath();
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

        ConfigureOptions(builder.Services, contentRoot);
        ConfigureServices(builder.Services);

        _host = builder.Build();
        _host.Start();
    }

    private static void ConfigureOptions(IServiceCollection services, string contentRoot)
    {
        services.Configure<PathOptions>(options =>
        {
            options.RootDirectory = contentRoot;
            options.SettingsDirectory = Path.Combine(contentRoot, "Settings");
            options.LogsDirectory = Path.Combine(contentRoot, "Logs");
            options.EnsureDirectoriesExist();
        });
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IFileConfig<PathOptions>, FileConfig>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddHostedService<HostBackgroundController>();

        // Serilog
        services.AddSingleton<ILoggerFactory, SerilogLoggerFactory>();
        services.AddSingleton<ITraceListenerFactory, SerilogTraceListenerFactory>();
        services.AddSingleton<ILogOutputSink, SerilogRichTextBoxSink>();

        // ZLogger
        // services.AddSingleton<ILoggerFactory, ZLoggerLoggerFactory>();
        // services.AddSingleton<ITraceListenerFactory, ZLoggerTraceListenerFactory>();
        // services.AddSingleton<ILogOutputSink, ZloggerRichTextBoxSink>();

        // Logging service
        services.AddSingleton<ILoggingService, LoggingService>();

        // Visualization Servers
        services.AddSingleton<BoundingBoxVisualizationServer>();
        services.AddSingleton<FaceVisualizationServer>();
        services.AddSingleton<MeshVisualizationServer>();
        services.AddSingleton<PolylineVisualizationServer>();
        services.AddSingleton<SolidVisualizationServer>();
        services.AddSingleton<XyzVisualizationServer>();

        // Visualization ViewModels
        services.AddSingleton<BoundingBoxVisualizationViewModel>();
        services.AddSingleton<FaceVisualizationViewModel>();
        services.AddSingleton<MeshVisualizationViewModel>();
        services.AddSingleton<PolylineVisualizationViewModel>();
        services.AddSingleton<SolidVisualizationViewModel>();
        services.AddSingleton<XyzVisualizationViewModel>();

        // Visualization Views
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

