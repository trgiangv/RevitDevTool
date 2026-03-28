using DevTools.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RevitDevTool.Controllers;
using RevitDevTool.Logging;
using RevitDevTool.Logging.Linkify;
using RevitDevTool.Settings;
using ZLogger.Scintilla.Public;
using RevitDevTool.Settings.Options;
using RevitDevTool.Utils;
using RevitDevTool.View;
using RevitDevTool.View.Settings;
using RevitDevTool.View.Settings.Visualization;
using RevitDevTool.ViewModel;
using RevitDevTool.ViewModel.Settings;
using RevitDevTool.ViewModel.Settings.Visualization;
using RevitDevTool.Visualization.Server;
using System.IO;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RevitDevTool.Execution.Interfaces;
using RevitDevTool.Execution.Providers;
using RevitDevTool.Execution.Providers.Dotnet;
using RevitDevTool.Execution.Services;
using RevitDevTool.Mcp;
using RevitDevTool.Mcp.Dotnet;
using RevitDevTool.Mcp.Interfaces;
using RevitDevTool.Mcp.Models;
using RevitDevTool.Mcp.Python;
using RevitDevTool.McpParser.Models;
// ReSharper disable ConvertToExtensionBlock

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
        
        builder.ConfigureOptions(contentRoot)
               .ConfigureLogging()
               .ConfigureServices();

        _host = builder.Build();
        _host.Start();
    }

    private static HostApplicationBuilder ConfigureOptions(this HostApplicationBuilder builder, string contentRoot)
    {
        builder.Services.Configure<PathOptions>(options =>
        {
            options.RootDirectory = contentRoot;
            options.SettingsDirectory = Path.Combine(contentRoot, "Settings");
            options.LogsDirectory = Path.Combine(contentRoot, "Logs");
            options.EnsureDirectoriesExist();
        });
        return builder;
    }
    
    private static HostApplicationBuilder ConfigureLogging(this HostApplicationBuilder builder)
    {
        var loggingConfig = new LoggingConfiguration();
        builder.Services.AddSingleton(loggingConfig);

        builder.Logging
            .AddConfiguration(loggingConfig.Configuration.GetSection("Logging"))
            .ClearProviders()
            .AddZLoggerScintillaWpf(
                v => v
                    .Channel(capacity: 50_000, flushMs: 50, maxBatch: 800)
                    .Display(maxLines: 50_000, fontSize: 9)
                    .WithLinkify(new RevitLinkifier()));

        return builder;
    }

    private static void ConfigureServices(this HostApplicationBuilder builder)
    {
        var services = builder.Services;
        // Core services
        services.AddSingleton<IFileConfig<PathOptions>, FileConfig>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddHostedService<HostBackgroundController>();

        // Logging
        services.TryAddSingleton<FileLogProcessor>();
        services.AddSingleton<IAppInfo, RevitAppInfo>();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<LogViewModel>();

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
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<GeneralSettingsViewModel>();
        services.AddSingleton<GeneralSettingsView>();
        services.AddSingleton<LogSettingsViewModel>();

        // Execution Services
        services.AddSingleton<ITreeStateManager, TreeStateManager>();
        services.AddSingleton<IFileWatcherService, FileWatcherService>();
        services.AddSingleton<IExecutionOrchestrator, ExecutionOrchestrator>();
        services.AddSingleton<IPackageService, PackageService>();

        // Execution Providers
        services.AddSingleton<IExecutionProvider, AssemblyExecutionProvider>();
        services.AddSingleton<IExecutionProvider, ScriptExecutionProvider>();
        services.AddKeyedSingleton<IExecutionProvider, AssemblyExecutionProvider>(ExecutionMode.Assembly);
        services.AddKeyedSingleton<IExecutionProvider, ScriptExecutionProvider>(ExecutionMode.Script);

        // Execution ViewModels
        services.AddSingleton<CommandViewModel>();
        services.AddSingleton<PackageViewModel>();
        services.AddSingleton<MemoryViewModel>();
        services.AddSingleton<ExecutionViewModel>();
        services.AddSingleton<CommandView>();
        services.AddSingleton<PackageView>();
        services.AddSingleton<MemoryView>();
        services.AddSingleton<ExecutionView>();
        
        // MCP
        services.AddSingleton<McpRegistryView>();
        services.AddSingleton<McpRegistryViewModel>();
        services.AddSingleton<BridgeConnectionState>();
        services.AddSingleton<DotnetMcpToolRegistryProvider>();
        services.AddSingleton<PythonMcpToolRegistryProvider>();
        services.AddSingleton<IMcpRegistryProvider>(sp => sp.GetRequiredService<DotnetMcpToolRegistryProvider>());
        services.AddSingleton<IMcpRegistryProvider>(sp => sp.GetRequiredService<PythonMcpToolRegistryProvider>());
        services.AddSingleton<ToolExecutionDispatcher>();
        services.AddSingleton<PrimitiveExecutionDispatcher>();
        services.AddSingleton<ToolRegistryStore>();
        services.AddHostedService<RevitPipeServer>();

        // Main
        services.AddSingleton<MainViewModel>();
        services.AddTransient<MainPage>();
        services.AddTransient<MainWindow>();
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
