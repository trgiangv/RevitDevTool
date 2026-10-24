using AcadDevTool.Adapters;
using AcadDevTool.Bridges;
using AcadDevTool.Controllers;
using AcadDevTool.Logging;
using AcadDevTool.Logging.Enrichers;
using AcadDevTool.Settings;
using AcadDevTool.View;
using CommunityToolkit.Mvvm.Messaging;
using DevTools.AssemblyIsolation;
using DevTools.Execution;
using DevTools.Execution.Interfaces;
using DevTools.Hosting;
using DevTools.Logging;
using DevTools.Logging.Abstractions;
using DevTools.Mcp.Acad.Resources;
using DevTools.Mcp.Acad.Tools;
using DevTools.Mcp.Hosting;
using DevTools.Mcp.Providers;
using DevTools.Presentation;
using DevTools.Presentation.Interfaces;
using DevTools.Settings;
using DevTools.Testing.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger.Scintilla.Public;

namespace AcadDevTool.Composition;

internal static class AcadServiceRegistration
{
    extension(HostApplicationBuilder builder)
    {
        internal HostApplicationBuilder AddSettingServices(string contentRoot)
        {
            var services = builder.Services;
            services.AddSettingServices(contentRoot);
            services.AddSingleton<SettingsService>();
            services.AddSingleton<IAcadSettingsService>(sp => sp.GetRequiredService<SettingsService>());
            services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
            return builder;
        }

        internal HostApplicationBuilder AddLoggingServices(Action<ScintillaOptions>? configureMonitor = null)
        {
            builder.AddLoggingProvider();
            builder.AddMonitorLogging(configureMonitor);
            builder.Logging.AddFilter("ModelContextProtocol", LogLevel.Warning);
#if RELEASE
        builder.Logging.SuppressHostingFrameworkLogs();
#endif

            var services = builder.Services;
            services.AddSingleton<LoggingService>();
            services.AddSingleton<ILoggingService>(sp => sp.GetRequiredService<LoggingService>());
            services.AddSingleton<IHostIdlingBridge, AcadIdlingBridge>();
            services.AddSingleton<ILogEnricherProvider, AcadLogEnricherProvider>();

            return builder;
        }

        internal HostApplicationBuilder AddApplicationServices()
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

        internal HostApplicationBuilder AddExecutionServices()
        {
            var services = builder.Services;

            services.AddSingleton<IHostContextExecutor, AcadHostContextExecutor>();
            services.AddSingleton<IDocumentBridge, AcadDocumentBridge>();
            services.AddSingleton<HostAssemblies, AcadHostAssemblies>();
            services.AddSingleton<ICommandRunner, AcadCommandRunner>();
            services.AddSingleton<ICommandDiscovery, AcadCommandDiscovery>();
            services.AddSingleton<ICompiledScriptBridge, AcadCompiledScriptBridge>();
            services.AddSingleton<IPythonBridge, AcadPythonBridge>();
            services.AddSingleton<IIronPythonBridge, AcadIronPythonBridge>();

            services.AddExecutionServices();
            services.AddMcpAdapter();
            services.AddTestingHostServices();

            services.AddSingleton<IBuiltInMcpResource, AcadCSharpCheatsheet>();
            services.AddSingleton<IBuiltInMcpResource, AcadPythonCheatsheet>();
            services.AddSingleton<AcadHistoryNavigator>();
            services.AddSingleton<IBuiltInMcpTool, NavigateHistoryTool>();
            services.AddSingleton<IBuiltInMcpTool, ViewScreenshotTool>();

            return builder;
        }
    }
}
