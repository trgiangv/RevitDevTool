using DevTools.Daemon.Auth;
using System.Text.Json;
using DevTools.Daemon.Composition;
using DevTools.Daemon.Desktop;
using DevTools.Daemon.Gateway;
using DevTools.Daemon.Tests.Support;
using DevTools.Settings.Configs;
using DevTools.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Moq;

namespace DevTools.Daemon.Tests;

public sealed class DesktopComponentTests
{
    [Theory]
    [InlineData(AppTheme.Light, true)]
    [InlineData(AppTheme.Dark, false)]
    public void ThemeHelper_IsLight_ReturnsFixedThemes(AppTheme theme, bool expected)
    {
        Assert.Equal(expected, ThemeHelper.IsLight(theme));
    }

    [Fact]
    public void ThemeHelper_IsLight_Auto_ReadsRegistry()
    {
        _ = ThemeHelper.IsLight(AppTheme.Auto);
    }

    [Fact]
    public void ThemeHelper_Apply_NoOpsWhenApplicationNotRunning()
    {
        ThemeHelper.Apply(AppTheme.Light);
        ThemeHelper.Apply(AppTheme.Dark);
        ThemeHelper.Apply(AppTheme.Auto);
    }

    [Fact]
    public void AppIcons_LoadEmbeddedResources()
    {
        Assert.NotNull(AppIcons.WindowIcon(true));
        Assert.NotNull(AppIcons.WindowIcon(false));
        using var dark = AppIcons.TrayIcon(true);
        using var light = AppIcons.TrayIcon(false);
        Assert.True(dark.Handle != 0);
        Assert.True(light.Handle != 0);
    }

    [Fact]
    public void SingleInstance_FirstInstanceIsUnique()
    {
        using var first = new SingleInstance();
        if (!first.IsFirstInstance)
            return;

        using var second = new SingleInstance();
        Assert.False(second.IsFirstInstance);
    }

    [Fact]
    public void AutoStart_EnableDisable_RoundTripsRegistryValue()
    {
        var runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        object? original = null;
        using (var key = Registry.CurrentUser.OpenSubKey(runKey))
            original = key?.GetValue(AppConstants.AutoStartValueName);

        try
        {
            AutoStart.Disable();
            Assert.False(AutoStart.IsEnabled);

            AutoStart.Enable();
            if (Environment.ProcessPath is not null)
                Assert.True(AutoStart.IsEnabled);

            AutoStart.Disable();
            Assert.False(AutoStart.IsEnabled);
        }
        finally
        {
            using var key = Registry.CurrentUser.OpenSubKey(runKey, writable: true);
            if (original is null)
                key?.DeleteValue(AppConstants.AutoStartValueName, throwOnMissingValue: false);
            else
                key?.SetValue(AppConstants.AutoStartValueName, original);
        }
    }

    [Fact]
    public void UserSettingsStore_Update_WritesAndReloadsSettings()
    {
        var settingsPath = UserSettings.FilePath;
        var backup = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;

        try
        {
            var store = DaemonTestDoubles.CreateUserSettingsStore(new UserSettings
            {
                Theme = AppTheme.Light,
                AutoStartEnabled = false,
            });

            store.Update(s =>
            {
                s.Theme = AppTheme.Dark;
                s.AutoStartEnabled = true;
            });

            var loaded = JsonSerializer.Deserialize(
                File.ReadAllText(settingsPath),
                UserSettingsJsonContext.Default.DictionaryStringUserSettings);
            Assert.Equal(AppTheme.Dark, loaded![UserSettings.SectionName].Theme);
            Assert.True(loaded[UserSettings.SectionName].AutoStartEnabled);
            Assert.Contains("\"Theme\"", File.ReadAllText(settingsPath), StringComparison.Ordinal);
        }
        finally
        {
            if (backup is null)
                File.Delete(settingsPath);
            else
                File.WriteAllText(settingsPath, backup);
        }
    }

    [Fact]
    public void AuthOptions_BuildsLoopbackUrls()
    {
        var options = new AuthOptions { LoopbackPort = 17899 };
        Assert.Equal("http://127.0.0.1:17899/", options.UriPrefix);
        Assert.Equal("http://127.0.0.1:17899/callback", options.RedirectUri);
    }

    [Fact]
    public void FileLogging_ConfiguresRollingFileProvider()
    {
        var folderName = $"daemon-log-{Guid.NewGuid():N}";
        var folder = Path.Combine(AppUtils.GetApplicationDataPath(), folderName);

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Logging:File:Folder"] = folderName,
                    ["Logging:File:RetentionDays"] = "0",
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging(logging => FileLogging.Configure(logging, configuration, clearProviders: true));
            using var provider = services.BuildServiceProvider();
            provider.GetRequiredService<ILoggerFactory>().CreateLogger("test").LogInformation("coverage");

            var logPath = Directory.GetFiles(folder, "log_*.log", SearchOption.AllDirectories);
            Assert.NotEmpty(logPath);
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task DiscoveryHostedService_StartsAndStops()
    {
        var discovery = new Mock<DevTools.Mcp.Client.IHostDiscovery>();
        discovery.Setup(d => d.RunAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct => Task.Delay(Timeout.Infinite, ct));

        var service = new DiscoveryHostedService(discovery.Object);
        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);
    }
}
