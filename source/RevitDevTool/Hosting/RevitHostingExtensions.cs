using CommunityToolkit.Mvvm.Messaging;
using DevTools.Agents.Revit.Prompts;
using DevTools.Agents.Revit.Resources;
using DevTools.Execution;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers;
using DevTools.Logging;
using DevTools.Logging.Abstractions;
using DevTools.Mcp.BuiltIn;
using DevTools.Presentation;
using DevTools.Presentation.Interfaces;
using DevTools.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Bridges;
using RevitDevTool.CommandBrowser;
using RevitDevTool.CommandBrowser.Services;
using RevitDevTool.CommandBrowser.ViewModels;
using RevitDevTool.Controllers;
using RevitDevTool.Core.Execution;
using RevitDevTool.HostAdapters;
using RevitDevTool.Execution;
using RevitDevTool.Logging;
using RevitDevTool.Logging.Enrichers;
using RevitDevTool.Settings;
using RevitDevTool.View;
using RevitDevTool.View.Settings.Visualization;
using RevitDevTool.ViewModel.Settings.Visualization;
using RevitDevTool.Visualization.Server;
using ZLogger.Scintilla.Public;
// ReSharper disable ConvertToExtensionBlock

namespace RevitDevTool.Hosting;

internal static class RevitHostingExtensions
{
    internal static HostApplicationBuilder AddSettingServices(this HostApplicationBuilder builder, string contentRoot)
    {
        var services = builder.Services;
        services.AddSettingServices(contentRoot);
        services.AddSingleton<SettingsService>();
        services.AddSingleton<IRevitSettingsService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
        return builder;
    }

    internal static HostApplicationBuilder AddLoggingServices(
        this HostApplicationBuilder builder,
        Action<ScintillaOptions>? configureMonitor = null)
    {
        builder.AddLoggingProvider(configureMonitor);

        var services = builder.Services;
        services.AddSingleton<LoggingService>();
        services.AddSingleton<ILoggingService>(sp => sp.GetRequiredService<LoggingService>());
        services.AddSingleton<IHostIdlingBridge, RevitIdlingBridge>();
        services.AddSingleton<ILogEnricherProvider, RevitLogEnricherProvider>();

        return builder;
    }

    internal static HostApplicationBuilder AddApplicationServices(this HostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddHostedService<HostBackgroundController>();
        services.AddSingleton<IHostAppInfo, RevitHostAppInfo>();
        services.AddSingleton<IContextEnricher>(sp =>
        {
            var settings = sp.GetRequiredService<IRevitSettingsService>();
            return new RevitContextProvider(settings.RevitEnrichers);
        });
        services.AddSingleton<PanelController>();

        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<MainPage>();
        services.AddTransient<MainWindow>();
        services.AddPresentationServices();

        services.AddSingleton<IVisualizationBridge, RevitVisualizationBridge>();
        
        // Servers
        services.AddSingleton<BoundingBoxVisualizationServer>();
        services.AddSingleton<FaceVisualizationServer>();
        services.AddSingleton<PlaneVisualizationServer>();
        services.AddSingleton<MeshVisualizationServer>();
        services.AddSingleton<PolylineVisualizationServer>();
        services.AddSingleton<SolidVisualizationServer>();
        services.AddSingleton<XyzVisualizationServer>();
        
        // ViewModels
        services.AddSingleton<BoundingBoxVisualizationViewModel>();
        services.AddSingleton<FaceVisualizationViewModel>();
        services.AddSingleton<MeshVisualizationViewModel>();
        services.AddSingleton<PolylineVisualizationViewModel>();
        services.AddSingleton<SolidVisualizationViewModel>();
        services.AddSingleton<XyzVisualizationViewModel>();
        
        // Views
        services.AddTransient<BoundingBoxVisualizationSettingsView>();
        services.AddTransient<FaceVisualizationSettingsView>();
        services.AddTransient<MeshVisualizationSettingsView>();
        services.AddTransient<PolylineVisualizationSettingsView>();
        services.AddTransient<SolidVisualizationSettingsView>();
        services.AddTransient<XyzVisualizationSettingsView>();
        
        // Command Browser
        services.AddSingleton<RibbonSnoopService>();
        services.AddSingleton<CommandBrowserCache>();
        services.AddSingleton<CommandBrowserViewModel>();
        services.AddSingleton<CommandBrowserController>();

        return builder;
    }

    internal static HostApplicationBuilder AddExecutionServices(this HostApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddSingleton<IDebuggerBridge, RevitDebuggerBridge>();
        services.AddSingleton<IExecutionGuard, ExecutionGuard>();
        services.AddSingleton<IHostContextExecutor, RevitHostContextExecutor>();
        services.AddSingleton<IDocumentBridge, RevitDocumentBridge>();
        services.AddSingleton<ICommandRunner, RevitCommandRunner>();
        services.AddSingleton<ICommandDiscovery, RevitCommandDiscovery>();
        services.AddSingleton<ICompiledScriptBridge, RevitCompiledScriptBridge>();
        services.AddSingleton<IPythonBridge, RevitPythonBridge>();
        services.AddSingleton<IIronPythonBridge, RevitIronPythonBridge>();

        services.AddExecutionServices(registerDefaultScriptProvider: false);
        services.AddSingleton<IScriptExecutionStrategyFactory, RevitScriptExecutionStrategyFactory>();
        services.AddSingleton<IExecutionProvider, ScriptExecutionProvider>();
        services.AddKeyedSingleton<IExecutionProvider, ScriptExecutionProvider>(ContainerMode.Script);

        services.AddSingleton<IBuiltInMcpResource, RevitCSharpCheatsheet>();
        services.AddSingleton<IBuiltInMcpResource, RevitPythonCheatsheet>();
        services.AddSingleton<IBuiltInMcpResource, RevitModelContext>();
        services.AddSingleton<IBuiltInMcpResource, RevitModelWarnings>();
        services.AddSingleton<IBuiltInMcpResource, RevitVersionInfo>();
        services.AddSingleton<IBuiltInMcpResource, RevitViewScreenshot>();
        services.AddSingleton<IBuiltInMcpPrompt, RevitCodePrompt>();
        services.AddSingleton<IBuiltInMcpTool, DevTools.Agents.Revit.Tools.UndoChangesTool>();

        return builder;
    }
}

