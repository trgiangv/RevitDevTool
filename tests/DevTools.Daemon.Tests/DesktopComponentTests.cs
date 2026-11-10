using DevTools.Daemon.Auth;
using DevTools.Daemon.Control;
using DevTools.Daemon.Desktop.Helpers;
using DevTools.Daemon.Desktop.Models;
using DevTools.Daemon.Desktop.Views;
using DevTools.Daemon.Logging;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Tests.Support;
using DevTools.Ipc;
using DevTools.Settings.Configs;
using DevTools.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Moq;
using System.Text.Json;

namespace DevTools.Daemon.Tests;

[TestClass]
public sealed class DesktopComponentTests
{
    [TestMethod]
    [DataRow(AppTheme.Light, true)]
    [DataRow(AppTheme.Dark, false)]
    public void ThemeHelper_IsLight_ReturnsFixedThemes(AppTheme theme, bool expected)
    {
        Assert.AreEqual(expected, ThemeHelper.IsLight(theme));
    }

    [TestMethod]
    public void ThemeHelper_IsLight_Auto_ReadsRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var expected = key?.GetValue("AppsUseLightTheme") is int value && value != 0;

        Assert.AreEqual(expected, ThemeHelper.IsLight(AppTheme.Auto));
    }

    [TestMethod]
    public void AppIcons_LoadEmbeddedResources()
    {
        Assert.IsNotNull(AppIcons.ForTheme(true));
        Assert.IsNotNull(AppIcons.ForTheme(false));
    }

    [TestMethod]
    public void SingleInstanceGuard_FirstInstanceIsUnique()
    {
        using var first = new SingleInstanceGuard();
        if (!first.IsFirstInstance)
            return;

        using var second = new SingleInstanceGuard();
        Assert.IsFalse(second.IsFirstInstance);
    }

    [TestMethod]
    public void SingleInstanceGuard_SecondStartAsksTheFirstToShow()
    {
        using var first = new SingleInstanceGuard();
        if (!first.IsFirstInstance)
            return;

        using var shown = new ManualResetEventSlim();
        first.OnShowRequested(shown.Set);
        SingleInstanceGuard.RequestShow();

        Assert.IsTrue(shown.Wait(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public void StartupRegistration_EnableDisable_RoundTripsRegistryValue()
    {
        using var state = new TempUserState();
        var startup = state.Startup;

        startup.Disable();
        Assert.IsFalse(startup.IsEnabled);

        startup.Enable(@"C:\RevitDevTool\DevTools.Daemon.exe");
        Assert.IsTrue(startup.IsEnabled);
        using (var key = Registry.CurrentUser.OpenSubKey(startup.KeyPath))
            Assert.AreEqual($"\"C:\\RevitDevTool\\DevTools.Daemon.exe\" {IpcConstants.DaemonArgs.Background}", key!.GetValue(AppConstants.AutoStartValueName));

        startup.Disable();
        Assert.IsFalse(startup.IsEnabled);
    }

    [TestMethod]
    public void UserSettingsStore_Update_WritesAndReloadsSettings()
    {
        using var state = new TempUserState();
        var store = DaemonTestDoubles.CreateUserSettingsStore(state.SettingsPath, new UserSettings
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
            File.ReadAllText(state.SettingsPath),
            UserSettingsJsonContext.Default.DictionaryStringUserSettings);
        Assert.AreEqual(AppTheme.Dark, loaded![UserSettings.SectionName].Theme);
        Assert.IsTrue(loaded[UserSettings.SectionName].AutoStartEnabled);
        Assert.Contains("\"Theme\"", File.ReadAllText(state.SettingsPath), StringComparison.Ordinal);
    }

    [TestMethod]
    public void AuthOptions_BuildsLoopbackUrls()
    {
        var options = new AuthOptions { LoopbackPort = 17899 };
        Assert.AreEqual("http://127.0.0.1:17899/", options.UriPrefix);
        Assert.AreEqual("http://127.0.0.1:17899/callback", options.RedirectUri);
    }

    [TestMethod]
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
            Assert.IsNotEmpty(logPath);
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* best-effort */ }
        }
    }

    [TestMethod]
    public async Task DiscoveryHostedService_StartsAndStops()
    {
        var running = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var discovery = new Mock<IHostConnections>();
        discovery.Setup(d => d.RunAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct =>
            {
                running.SetResult(ct);
                return Task.Delay(Timeout.Infinite, ct);
            });

        var service = new DiscoveryHostedService(discovery.Object);
        await service.StartAsync(CancellationToken.None);
        var token = await running.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await service.StopAsync(CancellationToken.None);

        Assert.IsTrue(token.IsCancellationRequested);
    }
}
