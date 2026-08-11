using CommunityToolkit.Mvvm.Messaging;
using DevTools.Agents.Acad.Resources;
using DevTools.Execution;
using DevTools.Execution.Interfaces;
using DevTools.Logging;
using DevTools.Logging.Abstractions;
using DevTools.Mcp.Catalog;
using DevTools.NUnit.Host;
using DevTools.Presentation;
using DevTools.Presentation.Interfaces;
using DevTools.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AcadDevTool.Bridges;
using AcadDevTool.Controllers;
using AcadDevTool.HostAdapters;
using AcadDevTool.Logging;
using AcadDevTool.Logging.Enrichers;
using AcadDevTool.Settings;
using DevTools.Agents.Acad.Tools;
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
        builder.AddLoggingProvider(configureMonitor);
        builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);

        var services = builder.Services;
        services.AddSingleton<LoggingService>();
        services.AddSingleton<ILoggingService>(sp => sp.GetRequiredService<LoggingService>());
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
        services.AddSingleton<IDocumentBridge, AcadDocumentBridge>();
        services.AddSingleton<ICommandRunner, AcadCommandRunner>();
        services.AddSingleton<ICommandDiscovery, AcadCommandDiscovery>();
        services.AddSingleton<ICompiledScriptBridge, AcadCompiledScriptBridge>();
        services.AddSingleton<IPythonBridge, AcadPythonBridge>();
        services.AddSingleton<IIronPythonBridge, AcadIronPythonBridge>();

        services.AddExecutionServices();
        services.AddNUnitHostServices();

        services.AddSingleton<IBuiltInMcpResource, AcadCSharpCheatsheet>();
        services.AddSingleton<IBuiltInMcpResource, AcadPythonCheatsheet>();
        services.AddSingleton<AcadHistoryNavigator>();
        services.AddSingleton<IBuiltInMcpTool, NavigateHistoryTool>();
        services.AddSingleton<IBuiltInMcpTool, ViewScreenshotTool>();

        return builder;
    }
}

