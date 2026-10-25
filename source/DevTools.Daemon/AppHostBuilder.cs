using DevTools.Daemon.Auth;
using DevTools.Daemon.Control;
using DevTools.Daemon.Desktop.Models;
using DevTools.Daemon.Desktop.Updates;
using DevTools.Daemon.Desktop.ViewModels;
using DevTools.Daemon.Desktop.Views;
using DevTools.Daemon.Logging;
using DevTools.Daemon.Mcp;
using DevTools.Daemon.Mcp.Hosting;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Tunnel;
using DevTools.FileMetadata.Acad;
using DevTools.FileMetadata.Core;
using DevTools.FileMetadata.Revit;
using DevTools.Hosting;
using DevTools.Hosting.Acad;
using DevTools.Hosting.Revit;
using DevTools.Update;
using DevTools.Update.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DevTools.Daemon;

public static class AppHostBuilder
{
    public static IHost CreateDesktop()
    {
        var builder = CreateBuilder();
        FileLogging.Configure(builder.Logging, builder.Configuration, clearProviders: false);

        builder.Services.AddSingleton<ControlHandler>();
        builder.Services.AddHostedService<ControlHostedService>();
        builder.Services.AddSingleton<TunnelHostedService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<TunnelHostedService>());
        builder.Services.AddSingleton<ITunnelStatusProvider>(sp => sp.GetRequiredService<TunnelHostedService>());
        AddUpdates(builder);
        builder.Services.AddSingleton<UpdateTray>();
        builder.Services.AddSingleton<UpdateNotices>();
        builder.Services.AddSingleton<IUpdateNotifier>(sp => sp.GetRequiredService<UpdateNotices>());
        builder.Services.AddSingleton<UpdatesViewModel>();
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddSingleton<MainWindow>();
        builder.Services.AddSingleton<TrayMenu>();

        return builder.Build();
    }

    /// <summary>
    ///     Headless host for <c>--select</c>: the update engine without UI, scheduler, or pipes.
    /// </summary>
    public static IHost CreateUpdateHost()
    {
        var builder = CreateBuilder();
        FileLogging.Configure(builder.Logging, builder.Configuration, clearProviders: true);
        AddUpdates(builder);
        return builder.Build();
    }

    private static void AddUpdates(HostApplicationBuilder builder)
    {
        builder.Services.Configure<UpdateEngineOptions>(builder.Configuration.GetSection(UpdateEngineOptions.SectionName));
        builder.Services.AddUpdateEngine();
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
            typeof(AppHostBuilder).Assembly.GetManifestResourceStream("appsettings.json")!);
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
        builder.Services.AddMcp();
        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddHostLaunchCore();
        builder.Services.AddRevitLaunch(RevitFileMetadataReader.TryReadRevitVersion);
        builder.Services.AddAcadLaunch();
        builder.Services.AddHostedService<DiscoveryHostedService>();

        return builder;
    }
}
