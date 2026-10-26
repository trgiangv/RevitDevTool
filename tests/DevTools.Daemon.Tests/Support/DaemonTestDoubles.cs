using DevTools.Daemon.Auth;
using DevTools.Daemon.Desktop.Models;
using DevTools.Daemon.Desktop.Updates;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Tunnel;
using DevTools.Ipc;
using DevTools.Update.Apply;
using DevTools.Update.Autodesk;
using DevTools.Update.Engine;
using DevTools.Update.Installations;
using DevTools.Update.Releases;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using Moq;

namespace DevTools.Daemon.Tests.Support;

internal static class DaemonTestDoubles
{
    public static UserSettingsStore CreateUserSettingsStore(
        UserSettings? settings = null,
        Action<ConfigurationBuilder>? configure = null)
    {
        settings ??= new UserSettings();
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["User:Theme"] = settings.Theme.ToString(),
            ["User:AutoStartEnabled"] = settings.AutoStartEnabled.ToString(),
        });
        configure?.Invoke(builder);
        var configuration = builder.Build();

        var monitor = new Mock<IOptionsMonitor<UserSettings>>();
        monitor.Setup(m => m.CurrentValue).Returns(settings);
        return new UserSettingsStore(monitor.Object, configuration);
    }

    public static Mock<IAuthService> CreateAuthService(
        bool authenticated = false,
        string? accessToken = null)
    {
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.IsAuthenticated).Returns(authenticated);
        auth.Setup(a => a.AccessToken).Returns(accessToken);
        auth.Setup(a => a.UserId).Returns(authenticated ? "user-1" : null);
        auth.Setup(a => a.Email).Returns(authenticated ? "user@example.com" : null);
        auth.Setup(a => a.DisplayName).Returns(authenticated ? "Test User" : null);
        auth.Setup(a => a.AvatarUrl).Returns((string?)null);
        auth.Setup(a => a.SignInAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthResult(true));
        auth.Setup(a => a.SignOutAsync()).Returns(Task.CompletedTask);
        auth.Setup(a => a.RefreshAsync()).ReturnsAsync(authenticated);
        return auth;
    }

    public static Mock<IHostConnections> CreateHostConnections(IReadOnlyList<ProcessCatalog>? entries = null)
    {
        entries ??= [];
        var catalog = new ProcessCatalogs();
        foreach (var entry in entries)
            catalog.Replace(entry);

        var broker = new Mock<IHostConnections>();
        broker.Setup(b => b.Catalog).Returns(catalog);
        broker.Setup(b => b.RunAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return broker;
    }

    public static ProcessCatalog CreateCatalogEntry(
        string hostApp,
        string version,
        int pid,
        string? pipeName = null)
    {
        pipeName ??= HostPipeName.FormatMcp(hostApp, version, pid);
        return new ProcessCatalog
        {
            ProcessId = pid,
            Instance = new InstanceInfo
            {
                HostApp = hostApp,
                VersionNumber = version,
                ProcessId = pid,
            },
            PipeName = pipeName,
            Tools = Array.Empty<Tool>(),
            Resources = Array.Empty<Resource>(),
            ResourceTemplates = Array.Empty<ResourceTemplate>(),
        };
    }

    public static Mock<IMcpPipeScanner> CreatePipeScanner(IReadOnlyCollection<string>? pipes = null)
    {
        var scanner = new Mock<IMcpPipeScanner>();
        scanner.Setup(s => s.Discover()).Returns(pipes ?? []);
        return scanner;
    }

    public static Mock<ITunnelStatusProvider> CreateTunnelStatus(TunnelStatus status = TunnelStatus.Disconnected)
    {
        var tunnel = new Mock<ITunnelStatusProvider>();
        tunnel.Setup(t => t.Status).Returns(status);
        return tunnel;
    }

    public static UpdatesViewModel CreateUpdates()
    {
        var bundle = new AutodeskBundle(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var reader = new ReleaseReader(new HttpClient(), new ReleaseSignature(null));
        var engine = new UpdateEngine(
            bundle,
            new InstallationStore(bundle),
            Mock.Of<IReleaseProvider>(),
            reader,
            new PayloadInstaller(bundle, Mock.Of<IFolderLock>(), reader),
            new DaemonUpdater(reader),
            Mock.Of<IUpdateNotifier>(),
            [],
            NullLogger<UpdateEngine>.Instance);
        return new UpdatesViewModel(engine);
    }
}
