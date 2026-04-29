using CommunityToolkit.Mvvm.Messaging;
using DevTools.Execution;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.Python;
using DevTools.Execution.Services;
using DevTools.Logging;
using DevTools.Logging.Abstractions;
using DevTools.Presentation;
using DevTools.Presentation.Interfaces;
using DevTools.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AcadDevTool.Bridges;
using AcadDevTool.Controllers;
using AcadDevTool.HostAdapters;
using AcadDevTool.Logging;
using AcadDevTool.Logging.Enrichers;
using AcadDevTool.Settings;
using AcadDevTool.View;
using ZLogger.Scintilla.Public;
// ReSharper disable ConvertToExtensionBlock

namespace AcadDevTool.Hosting;

internal static class AcadHostingExtensions
{
    internal static HostApplicationBuilder AddSettingServices(this HostApplicationBuilder builder, string contentRoot)
    {
        var services = builder.Services;
        services.AddSettingServices(contentRoot);
        services.AddSingleton<SettingsService>();
        services.AddSingleton<IAcadSettingsService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
        return builder;
    }

    internal static HostApplicationBuilder AddLoggingServices(
        this HostApplicationBuilder builder,
        Action<ScintillaOptions>? configureMonitor = null)
    {
        LoggingExtensions.AddLoggingServices(builder, configureMonitor);

        var services = builder.Services;
        services.AddSingleton<AcadLoggingService>();
        services.AddSingleton<IAcadLoggingService>(sp => sp.GetRequiredService<AcadLoggingService>());
        services.AddSingleton<IDevToolsLoggingService>(sp => sp.GetRequiredService<AcadLoggingService>());
        services.AddSingleton<IHostIdlingBridge, AcadIdlingBridge>();
        services.AddSingleton<ILogEnricherProvider, AcadLogEnricherProvider>();

        return builder;
    }

    internal static HostApplicationBuilder AddApplicationServices(this HostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddHostedService<HostBackgroundController>();
        services.AddSingleton<IHostAppInfo, AcadHostAppInfo>();
        services.AddSingleton<IContextEnricher>(sp =>
        {
            var settings = sp.GetRequiredService<IAcadSettingsService>();
            return new AcadContextProvider(settings.AcadEnrichers);
        });
        services.AddSingleton<PanelController>();

        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddTransient<MainPage>();
        services.AddPresentationServices();

        return builder;
    }

    internal static HostApplicationBuilder AddExecutionServices(this HostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddSingleton<IDebuggerBridge, AcadDebuggerBridge>();
        services.AddSingleton<IHostContextExecutor, AcadHostContextExecutor>();
        services.AddSingleton<ICommandRunner, AcadCommandRunner>();
        services.AddSingleton<ICommandDiscovery, AcadCommandDiscovery>();
        services.AddSingleton<IFSharpHostSupport, AcadFSharpSupport>();
        services.AddSingleton<IHostPythonBridge, AcadPythonBridge>();

        NetworkService.ConfigureUserAgent("AutoCAD");
        PythonEmbedded.Configure(PythonHostKind.AutoCad);

        services.AddExecutionServices();
        return builder;
    }
}

