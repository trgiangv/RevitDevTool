using DevTools.Daemon.Auth;
using DevTools.Daemon.Dashboard;
using DevTools.Daemon.Mcp;
using DevTools.Daemon.Tray;
using DevTools.Mcp.Routing.Catalog;
using DevTools.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Daemon.Hosting;

public static class DaemonHostBuilder
{
    private const string EmbeddedSettingsResource = "appsettings.json";
    private const string DevelopmentSettingsFile = "appsettings.development.json";
    private const string TrayLogFile = "daemon-tray.log";
    private const string StdioLogFile = "daemon-stdio.log";

    public static IHost CreateTrayHost(SingleInstance singleInstance)
    {
        var builder = CreateBuilder();

        builder.Logging.AddZLoggerFile(
            Path.Combine(AppUtils.GetApplicationDataPath(), TrayLogFile));

        builder.Services.AddSingleton(singleInstance);
        builder.Services.AddHostedService<ControlPipeHostedService>();
        builder.Services.AddSingleton<GatewayHostedService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<GatewayHostedService>());
        builder.Services.AddSingleton<ITunnelStatusProvider>(sp => sp.GetRequiredService<GatewayHostedService>());
        builder.Services.AddSingleton<TrayViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<DashboardWindow>();

        return builder.Build();
    }

    public static IHost CreateStdioHost()
    {
        var builder = CreateBuilder();

        builder.Logging.ClearProviders();
        builder.Logging.AddZLoggerFile(
            Path.Combine(AppUtils.GetApplicationDataPath(), StdioLogFile));

        builder.Services.AddHostedService<StdioHostedService>();
        return builder.Build();
    }

    private static HostApplicationBuilder CreateBuilder()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = []
        });

        builder.Configuration.AddJsonStream(
            typeof(DaemonHostBuilder).Assembly.GetManifestResourceStream(EmbeddedSettingsResource)!);
        builder.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, DevelopmentSettingsFile),
            optional: true, reloadOnChange: false);

        builder.Services.Configure<AuthOptions>(
            builder.Configuration.GetSection(AuthOptions.SectionName));
        builder.Services.Configure<GatewayOptions>(
            builder.Configuration.GetSection(GatewayOptions.SectionName));

        builder.Services.AddSingleton(_ => DaemonSettings.Load());
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<InstanceManager>();
        builder.Services.AddSingleton<DynamicToolCatalog>();
        builder.Services.AddSingleton<McpEngine>();
        builder.Services.AddSingleton<ControlPipeHandler>();
        builder.Services.AddHostedService<DiscoveryHostedService>();

        return builder;
    }
}
