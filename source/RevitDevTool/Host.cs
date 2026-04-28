using CommunityToolkit.Mvvm.Messaging;
using DevTools.Logging;
using DevTools.Logging.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RevitDevTool.Bridges;
using RevitDevTool.Controllers;
using RevitDevTool.Logging;
using RevitDevTool.Logging.Enrichers;
using RevitDevTool.Logging.Linkify;
using RevitDevTool.Settings;
using DevTools.Execution.Settings;
using RevitDevTool.View;
using RevitDevTool.View.Settings.Visualization;
using RevitDevTool.ViewModel.Settings.Visualization;
using RevitDevTool.Visualization.Server;
using System.IO;
using DevTools.Execution;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Logging.Abstractions;
using DevTools.McpParser.Dotnet;
using DevTools.UI.Theme;
using DevTools.Utilities;
using DevTools.Views;
using DevTools.Views.Interfaces;
using DevTools.Views.ViewModel;
using DevTools.Views.ViewModel.Settings;
using RevitDevTool.HostAdapters;
// ReSharper disable ConvertToExtensionBlock

namespace RevitDevTool;

public static class Host
{
    private static IHost? _host;

    public static void Start()
    {
        SetupTheme();
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
        ViewServiceLocator.Services = _host.Services;
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
            .AddMonitorLogging(v => v
                .Channel(capacity: 50_000, flushMs: 50, maxBatch: 800)
                .Display(maxLines: 50_000, fontSize: 9)
                .WithLinkify(new RevitLinkifier()))
            .AddFileLogging()
            .AddHttpLogging();

        return builder;
    }

    private static void ConfigureServices(this HostApplicationBuilder builder)
    {
        var services = builder.Services;

        // Messaging
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        // Core services
        services.AddSingleton<IFileConfig<PathOptions>, FileConfig>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<IRevitSettingsService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton<IDevToolsSettingsService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddHostedService<HostBackgroundController>();

        // Logging
        services.AddSingleton<IHostAppInfo, RevitHostAppInfo>();
        services.AddSingleton<IContextEnricher>(sp =>
        {
            var settings = sp.GetRequiredService<IRevitSettingsService>();
            return new RevitContextProvider(settings.RevitEnrichers);
        });
        services.AddSingleton<LoggingService>();
        services.AddSingleton<IDevToolsLoggingService>(sp => sp.GetRequiredService<LoggingService>());
        services.AddSingleton<PanelController>();

        // Bridges
        services.AddSingleton<IDebuggerBridge, RevitDebuggerBridge>();
        services.AddSingleton<IVisualizationBridge, RevitVisualizationBridge>();
        services.AddSingleton<IHostIdlingBridge, RevitIdlingBridge>();
        services.AddSingleton<ILogEnricherProvider, RevitLogEnricherProvider>();

        // Visualization Servers
        services.AddSingleton<BoundingBoxVisualizationServer>();
        services.AddSingleton<FaceVisualizationServer>();
        services.AddSingleton<PlaneVisualizationServer>();
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
        services.AddTransient<BoundingBoxVisualizationSettingsView>();
        services.AddTransient<FaceVisualizationSettingsView>();
        services.AddTransient<MeshVisualizationSettingsView>();
        services.AddTransient<PolylineVisualizationSettingsView>();
        services.AddTransient<SolidVisualizationSettingsView>();
        services.AddTransient<XyzVisualizationSettingsView>();

        // Shared ViewModels from DevTools.Views
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<GeneralSettingsViewModel>();
        services.AddSingleton<LogSettingsViewModel>();
        services.AddSingleton<LogViewModel>();
        services.AddSingleton<CommandViewModel>();
        services.AddSingleton<PackageViewModel>();
        services.AddSingleton<MemoryViewModel>();
        services.AddSingleton<ExecutionViewModel>();
        services.AddSingleton<McpRegistryViewModel>();
        services.AddSingleton<MainViewModel>(sp => new MainViewModel(
            sp.GetRequiredService<LogViewModel>(),
            sp.GetRequiredService<DevTools.Views.View.ExecutionView>(),
            sp.GetRequiredService<DevTools.Views.View.McpRegistryView>(),
            sp.GetRequiredService<DevTools.Views.View.MemoryView>(),
            sp.GetRequiredService<LogSettingsViewModel>(),
            sp.GetRequiredService<IDevToolsSettingsService>()));

        // Host-specific execution adapters
        services.AddSingleton<IHostContextExecutor, RevitHostContextExecutor>();
        services.AddSingleton<ICommandRunner, RevitCommandRunner>();
        services.AddSingleton<ICommandDiscovery, RevitCommandDiscovery>();
        services.AddSingleton<IFSharpHostSupport, RevitFSharpSupport>();
        services.AddSingleton<IHostPythonBridge, RevitPythonBridge>();

        // Configure shared services for Revit host
        NetworkService.ConfigureUserAgent("Revit");
        PythonEmbedded.Configure(PythonHostKind.Revit);

        // Shared execution services (Python, FSharp, Orchestrator, Pipe Server, MCP, etc.)
        services.AddDevToolsExecution();

        // Shared Views from DevTools.Views
        services.AddSingleton<DevTools.Views.View.CommandView>();
        services.AddSingleton<DevTools.Views.View.PackageView>();
        services.AddSingleton<DevTools.Views.View.MemoryView>();
        services.AddSingleton<DevTools.Views.View.ExecutionView>();
        services.AddSingleton<DevTools.Views.View.McpRegistryView>();
        services.AddSingleton<McpToolsetContextManager>();
        services.AddSingleton<DotnetMethodResolver>();

        // Main (host-specific)
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

    private static void SetupTheme()
    {
#if REVIT2024_OR_GREATER
        ThemeManager.Setup(
            () => UIThemeManager.CurrentTheme == UITheme.Dark
                ? AppTheme.Dark
                : AppTheme.Light,
            onChanged => UIFramework.ApplicationTheme.CurrentTheme.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(UIFramework.ApplicationTheme.CurrentTheme.RibbonPanelBackgroundBrush)) return;
                if (UIThemeManager.CurrentTheme.ToString() == UIFramework.ApplicationTheme.CurrentTheme.RibbonTheme.Name) return;
                DispatcherHelper.RunOnMainThread(onChanged);
            });
#endif
    }
}
