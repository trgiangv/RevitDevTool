using DevTools.Daemon.Auth;
using DevTools.Daemon.Desktop;
using DevTools.Daemon.Gateway;
using DevTools.Ipc;
using DevTools.Mcp.Client;
using DevTools.Mcp.Core.Sessions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using ModelContextProtocol.Protocol;

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

    public static Mock<IHostBroker> CreateHostBroker(IReadOnlyList<HostCatalogEntry>? entries = null)
    {
        entries ??= [];
        var catalog = new Mock<IConnectedHostCatalog>();
        catalog.Setup(c => c.List()).Returns(entries);

        var broker = new Mock<IHostBroker>();
        broker.Setup(b => b.Catalog).Returns(catalog.Object);
        return broker;
    }

    public static HostCatalogEntry CreateCatalogEntry(
        string hostApp,
        string version,
        int pid,
        string? pipeName = null)
    {
        pipeName ??= HostPipeName.FormatMcp(hostApp, version, pid);
        return new HostCatalogEntry
        {
            Key = new HostKey("machine-1", pid),
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
}
