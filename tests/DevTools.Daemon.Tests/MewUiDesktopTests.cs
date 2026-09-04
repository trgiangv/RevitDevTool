using Aprillz.MewUI;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Desktop;
using DevTools.Daemon.Gateway;
using DevTools.Daemon.Tests.Support;
using DevTools.Daemon.Views;
using DevTools.Settings.Configs;
using Microsoft.Win32;
using Moq;

namespace DevTools.Daemon.Tests;

[Collection(nameof(MewUiApplicationCollection))]
public sealed class MewUiDesktopTests(MewUiSession session) : MewUiApplicationTestBase(session)
{
    [Fact]
    public void Views_ConstructAndBuildMarkup()
    {
        RunOnUi(() =>
        {
            var state = CreateAppState();
            using var window = new MainWindow(state);
            window.Show();

            Assert.Equal("DevTools Daemon", window.Title);
            Assert.NotNull(window.Icon);

            _ = new OverviewView(state);
            _ = new HostsView(state.Hosts);
            _ = new SettingsView(state.Preferences, state.Version);
        });
    }

    [Fact]
    public void ThemeHelper_Apply_UpdatesWhenApplicationRunning()
    {
        RunOnUi(() =>
        {
            ThemeHelper.Apply(AppTheme.Light);
            ThemeHelper.Apply(AppTheme.Dark);
            ThemeHelper.Apply(AppTheme.Auto);

            var fired = false;
            ThemeHelper.Changed += () => fired = true;
            ThemeHelper.Apply(AppTheme.Light);
            Assert.True(fired);
        });
    }

    [Fact]
    public void Preferences_ThemeChange_PersistsSettings()
    {
        RunOnUi(() =>
        {
            var settingsPath = UserSettings.FilePath;
            var backup = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
            try
            {
                var store = DaemonTestDoubles.CreateUserSettingsStore(new UserSettings { Theme = AppTheme.Light });
                var preferences = new Preferences(store);

                preferences.Theme.Value = AppTheme.Dark;
                Assert.Equal(AppTheme.Dark, preferences.Theme.Value);
            }
            finally
            {
                if (backup is null)
                    File.Delete(settingsPath);
                else
                    File.WriteAllText(settingsPath, backup);
            }
        });
    }

    [Fact]
    public void AppState_RefreshAuthAndGatewayStatus()
    {
        RunOnUi(() =>
        {
            var auth = DaemonTestDoubles.CreateAuthService(authenticated: true);
            var tunnel = DaemonTestDoubles.CreateTunnelStatus(TunnelStatus.Connected);
            var state = CreateAppState(auth.Object, tunnel: tunnel.Object);

            Assert.True(state.IsAuthenticated.Value);
            Assert.Equal("Connected", state.GatewayStatus.Value);
            Assert.Equal("Test User", state.DisplayName.Value);

            tunnel.Raise(t => t.StatusChanged += null!, new object(), new TunnelStatusChangedArgs(TunnelStatus.Reconnecting));
            Assert.Equal("Reconnecting...", state.GatewayStatus.Value);

            state.SelectedTabIndex.Value = 1;
            state.SelectedTabIndex.Value = 2;
        });
    }

    [Fact]
    public void AppState_SignOut_RefreshesState()
    {
        var auth = DaemonTestDoubles.CreateAuthService(authenticated: true);
        RunOnUiAsync(async () =>
        {
            var state = CreateAppState(auth.Object);
            await state.SignOut();
            auth.Verify(a => a.SignOutAsync(), Times.Once);
        });
    }

    [Fact]
    public void MainWindow_Close_HidesInsteadOfClosing()
    {
        RunOnUi(() =>
        {
            var state = CreateAppState();
            using var window = new MainWindow(state);
            window.Show();
            window.Close();
            Assert.True(window.IsVisible);
        });
    }

    [Fact]
    public void AppState_LoadAvatar_InvalidUrl_ClearsImage()
    {
        RunOnUi(() =>
        {
            var auth = DaemonTestDoubles.CreateAuthService(authenticated: true);
            auth.Setup(a => a.AvatarUrl).Returns("http://127.0.0.1:9/avatar.png");
            var state = CreateAppState(auth.Object);
            Assert.Null(state.AvatarImage.Value);
        });
    }

    [Fact]
    public void TrayMenu_StartAndDispose_DoesNotThrow()
    {
        RunOnUi(() =>
        {
            var state = CreateAppState();
            using var window = new MainWindow(state);
            using var tray = new TrayMenu(state, window);
            tray.Start();
            tray.ShowMainWindow();
            tray.Dispose();
        });
    }

    [Fact]
    public void Preferences_AutoStartToggle_UpdatesRegistry()
    {
        RunOnUi(() =>
        {
            var runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            object? original = null;
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey))
                original = key?.GetValue(AppConstants.AutoStartValueName);

            try
            {
                var store = DaemonTestDoubles.CreateUserSettingsStore();
                var preferences = new Preferences(store);
                preferences.AutoStartEnabled.Value = true;
                if (Environment.ProcessPath is not null)
                    Assert.True(AutoStart.IsEnabled);
                preferences.AutoStartEnabled.Value = false;
                Assert.False(AutoStart.IsEnabled);
            }
            finally
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey, writable: true);
                if (original is null)
                    key?.DeleteValue(AppConstants.AutoStartValueName, throwOnMissingValue: false);
                else
                    key?.SetValue(AppConstants.AutoStartValueName, original);
            }
        });
    }

    [Fact]
    public void AppState_NotAuthenticated_ShowsSignedOutGatewayStatus()
    {
        RunOnUi(() =>
        {
            var auth = DaemonTestDoubles.CreateAuthService(authenticated: false);
            var state = CreateAppState(auth.Object);
            Assert.Equal("Not signed in", state.GatewayStatus.Value);
        });
    }

    [Fact]
    public void UiDispatch_PostAndSend_ExecuteOnUiThread()
    {
        RunOnUi(() =>
        {
            var executed = false;
            UiDispatch.Post(() => executed = true);
            Assert.True(executed);

            UiDispatch.Send(() => executed = false);
            Assert.False(executed);
        });
    }

    private static AppState CreateAppState(
        IAuthService? auth = null,
        ITunnelStatusProvider? tunnel = null)
    {
        auth ??= DaemonTestDoubles.CreateAuthService().Object;
        tunnel ??= DaemonTestDoubles.CreateTunnelStatus().Object;
        var broker = DaemonTestDoubles.CreateHostBroker().Object;
        var scanner = DaemonTestDoubles.CreatePipeScanner().Object;
        var store = DaemonTestDoubles.CreateUserSettingsStore();
        return new AppState(auth, broker, scanner, store, tunnel);
    }
}
