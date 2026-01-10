using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Logging;
using RevitDevTool.Logging.Serilog;
using RevitDevTool.Services;
using RevitDevTool.View.Settings;
using RevitDevTool.View.Settings.Visualization;
using RevitDevTool.ViewModel;
using RevitDevTool.ViewModel.Settings;
using RevitDevTool.ViewModel.Settings.Visualization;

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
        
        services.AddSingleton<ILoggerFactory, SerilogLoggerFactory>();
        services.AddSingleton<ITraceListenerFactory, SerilogTraceListenerFactory>();

        services.AddSingleton<TraceLogViewModel>();
        services.AddTransient<TraceLogPageViewModel>();

        services.AddTransient<GeneralSettingsViewModel>();
        services.AddTransient<GeneralSettingsView>();
        services.AddSingleton<LogSettingsViewModel>();
        
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
