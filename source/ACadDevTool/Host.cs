using AcadDevTool.Bridges;
using AcadDevTool.Controllers;
using AcadDevTool.HostAdapters;
using AcadDevTool.Logging;
using AcadDevTool.Logging.Enrichers;
using AcadDevTool.Settings;
using AcadDevTool.View;
using CommunityToolkit.Mvvm.Messaging;
using DevTools.Execution;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Execution.Settings;
using DevTools.Logging;
using DevTools.Logging.Abstractions;
using DevTools.Logging.Extensions;
using DevTools.McpParser.Dotnet;
using DevTools.UI.Theme;
using DevTools.Utilities;
using DevTools.Presentation;
using DevTools.Presentation.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using DevTools.Presentation.ViewModels;
using DevTools.Presentation.ViewModels.Settings;
using DevTools.Presentation.Views;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace AcadDevTool;

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
                .Display(maxLines: 50_000, fontSize: 9))
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
        services.AddSingleton<IAcadSettingsService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton<IDevToolsSettingsService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddHostedService<HostBackgroundController>();

        // Logging
        services.AddSingleton<IHostAppInfo, AcadHostAppInfo>();
        services.AddSingleton<IContextEnricher>(sp =>
        {
            var settings = sp.GetRequiredService<IAcadSettingsService>();
            return new AcadContextProvider(settings.AcadEnrichers);
        });
        services.AddSingleton<AcadLoggingService>();
        services.AddSingleton<IAcadLoggingService>(sp => sp.GetRequiredService<AcadLoggingService>());
        services.AddSingleton<IDevToolsLoggingService>(sp => sp.GetRequiredService<AcadLoggingService>());
        services.AddSingleton<PanelController>();

        // Bridges
        services.AddSingleton<IDebuggerBridge, AcadDebuggerBridge>();
        services.AddSingleton<IHostIdlingBridge, AcadIdlingBridge>();
        services.AddSingleton<ILogEnricherProvider, AcadLogEnricherProvider>();

        // Shared ViewModels from DevTools.Presentation
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
            sp.GetRequiredService<ExecutionView>(),
            sp.GetRequiredService<McpRegistryView>(),
            sp.GetRequiredService<MemoryView>(),
            sp.GetRequiredService<LogSettingsViewModel>(),
            sp.GetRequiredService<IDevToolsSettingsService>()));

        // Host-specific execution adapters
        services.AddSingleton<IHostContextExecutor, AcadHostContextExecutor>();
        services.AddSingleton<ICommandRunner, AcadCommandRunner>();
        services.AddSingleton<ICommandDiscovery, AcadCommandDiscovery>();
        services.AddSingleton<IFSharpHostSupport, AcadFSharpSupport>();
        services.AddSingleton<IHostPythonBridge, AcadPythonBridge>();

        // Configure shared services for AutoCAD host
        NetworkService.ConfigureUserAgent("AutoCAD");
        PythonEmbedded.Configure(PythonHostKind.AutoCAD);

        // Shared execution services
        services.AddDevToolsExecution();

        // Shared Views from DevTools.Presentation
        services.AddSingleton<CommandView>();
        services.AddSingleton<PackageView>();
        services.AddSingleton<MemoryView>();
        services.AddSingleton<ExecutionView>();
        services.AddSingleton<McpRegistryView>();
        services.AddSingleton<McpToolsetContextManager>();
        services.AddSingleton<DotnetMethodResolver>();

        // Main (host-specific)
        services.AddTransient<MainPage>();
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
        ThemeManager.Setup(
            () => (short)AcadApp.GetSystemVariable("COLORTHEME") == 0 ? AppTheme.Dark : AppTheme.Light,
            onChanged => AcadApp.SystemVariableChanged += (_, e) =>
            {
                if (!string.Equals(e.Name, "COLORTHEME", StringComparison.OrdinalIgnoreCase)) return;
                DispatcherHelper.RunOnMainThread(onChanged);
            });
    }
}
