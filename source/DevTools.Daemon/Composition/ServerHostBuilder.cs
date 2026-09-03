using DevTools.Daemon.Auth;
using DevTools.Daemon.Control;
using DevTools.Daemon.Desktop;
using DevTools.Daemon.Gateway;
using DevTools.Daemon.Tools;
using DevTools.Daemon.Views;
using DevTools.FileMetadata.Acad;
using DevTools.FileMetadata.Core;
using DevTools.FileMetadata.Revit;
using DevTools.Hosting;
using DevTools.Hosting.Acad;
using DevTools.Hosting.Revit;
using DevTools.Mcp.Catalog;
using DevTools.Mcp.Client;
using DevTools.Mcp.Server.Contracts;
using DevTools.Mcp.Server.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DevTools.Daemon.Composition;

public static class ServerHostBuilder
{
    private const string EmbeddedSettingsResource = "appsettings.json";
    private const string DevelopmentSettingsFile = "appsettings.development.json";

    public static IHost CreateDesktop()
    {
        var builder = CreateBuilder();
        FileLogging.Configure(builder.Logging, builder.Configuration, clearProviders: false);

        builder.Services.AddSingleton<ControlPipeHandler>();
        builder.Services.AddHostedService<ControlPipeHostedService>();
        builder.Services.AddSingleton<GatewayHostedService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<GatewayHostedService>());
        builder.Services.AddSingleton<ITunnelStatusProvider>(sp => sp.GetRequiredService<GatewayHostedService>());
        builder.Services.AddSingleton<AppState>();
        builder.Services.AddSingleton<MainWindow>();
        builder.Services.AddSingleton<TrayMenu>();

        return builder.Build();
    }

    public static IHost CreateStdioHost(string[]? args = null)
    {
        var builder = CreateBuilder(args);
        // stdout is reserved for MCP JSON-RPC — file logging only.
        FileLogging.Configure(builder.Logging, builder.Configuration, clearProviders: true);

        builder.Services.AddHostedService<StdioHostedService>();
        return builder.Build();
    }

    internal static IHost CreateStdioHostForTests()
    {
        var builder = CreateBuilder();
        return builder.Build();
    }

    private static HostApplicationBuilder CreateBuilder(string[]? args = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = args ?? []
        });

        builder.Configuration.AddJsonStream(
            typeof(ServerHostBuilder).Assembly.GetManifestResourceStream(EmbeddedSettingsResource)!);
        builder.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, DevelopmentSettingsFile),
            optional: true, reloadOnChange: false);
        builder.Configuration.AddJsonFile(
            UserSettings.FilePath, optional: true, reloadOnChange: true);

        builder.Services.Configure<AuthOptions>(
            builder.Configuration.GetSection(AuthOptions.SectionName));
        builder.Services.Configure<GatewayOptions>(
            builder.Configuration.GetSection(GatewayOptions.SectionName));
        builder.Services.Configure<UserSettings>(
            builder.Configuration.GetSection(UserSettings.SectionName));
        builder.Services.AddSingleton<UserSettingsStore>();
        builder.Services
            .AddFileMetadataReaders()
            .AddRevitFileMetadataReader()
            .AddAcadFileMetadataReader();
        builder.Services
            .AddMcp()
            .AddMcpHostClient();
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IMachineLister, MachineLister>();
        builder.Services.AddHostLaunchCore();
        builder.Services.AddRevitLaunch(RevitFileMetadataReader.TryReadRevitVersion);
        builder.Services.AddAcadLaunch();
        builder.Services.AddSingleton<McpEngine>();
        builder.Services.AddHostedService<DiscoveryHostedService>();

        return builder;
    }
}
