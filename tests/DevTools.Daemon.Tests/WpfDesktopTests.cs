using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Desktop;
using DevTools.Daemon.Gateway;
using DevTools.Daemon.Tests.Support;
using DevTools.Daemon.Views;
using DevTools.Settings.Configs;
using Moq;

namespace DevTools.Daemon.Tests;

[DoNotParallelize]
[TestClass]
public sealed class WpfDesktopTests : WpfApplicationTestBase
{
    [TestMethod]
    public void Views_ConstructAndShow()
    {
        RunOnUi(() =>
        {
            var state = CreateAppState();
            var window = new MainWindow(state);
            window.Show();

            Assert.AreEqual("DevTools Daemon", window.Title);
            Assert.AreEqual(400, window.Width);
            Assert.AreEqual(400, window.Height);
            Assert.AreEqual(300, window.MinWidth);
            Assert.AreEqual(300, window.MinHeight);
            Assert.IsNotNull(window.Icon);

            _ = new OverviewView { DataContext = state };
            _ = new HostsView { DataContext = state };
            _ = new SettingsView { DataContext = state };
        });
    }

    [TestMethod]
    public void AppState_CreateAvatar_DecodesImageBytes()
    {
        RunOnUi(() =>
        {
            var png = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
            var image = AppState.CreateAvatar(png);
            Assert.IsNotNull(image);
            Assert.IsInstanceOfType<BitmapImage>(image);
            Assert.AreEqual(1, ((BitmapImage)image).PixelWidth);
        });
    }

    [TestMethod]
    public void ThemeHelper_Apply_UpdatesWhenApplicationRunning()
    {
        RunOnUi(() =>
        {
            ThemeHelper.Apply(AppTheme.Light);
            var light = (Color)Application.Current.FindResource("PrimaryTextColor");
            ThemeHelper.Apply(AppTheme.Dark);
            var dark = (Color)Application.Current.FindResource("PrimaryTextColor");
            var darkBrush = (SolidColorBrush)Application.Current.FindResource("PrimaryTextBrush");
            Assert.AreEqual(Colors.White, dark);
            Assert.AreNotEqual(light, dark);
            Assert.AreEqual(dark, darkBrush.Color);

            var state = CreateAppState();
            var window = new MainWindow(state);
            window.Show();
            ThemeHelper.Apply(AppTheme.Dark);
            var background = (SolidColorBrush)window.Background;
            Assert.AreEqual((Color)Application.Current.FindResource("SecondaryRegionColor"), background.Color);
            ThemeHelper.Apply(AppTheme.Auto);

            var fired = false;
            ThemeHelper.Changed += () => fired = true;
            ThemeHelper.Apply(AppTheme.Light);
            Assert.IsTrue(fired);
        });
    }

    [TestMethod]
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

                preferences.Theme = AppTheme.Dark;
                Assert.AreEqual(AppTheme.Dark, preferences.Theme);
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

    [TestMethod]
    public void AppState_RefreshAuthAndGatewayStatus()
    {
        RunOnUi(() =>
        {
            var auth = DaemonTestDoubles.CreateAuthService(authenticated: true);
            var tunnel = DaemonTestDoubles.CreateTunnelStatus(TunnelStatus.Connected);
            var state = CreateAppState(auth.Object, tunnel: tunnel.Object);

            Assert.IsTrue(state.IsAuthenticated);
            Assert.AreEqual("Connected", state.GatewayStatus);
            Assert.AreEqual("Test User", state.DisplayName);

            tunnel.Raise(t => t.StatusChanged += null!, new object(), new TunnelStatusChangedArgs(TunnelStatus.Reconnecting));
            Assert.AreEqual("Reconnecting...", state.GatewayStatus);

            state.SelectedTabIndex = 1;
            state.SelectedTabIndex = 2;
        });
    }

    [TestMethod]
    public void AppState_SignInCommand_CallsAuthService()
    {
        var auth = DaemonTestDoubles.CreateAuthService();
        RunOnUiAsync(async () =>
        {
            var state = CreateAppState(auth.Object);
            Assert.IsTrue(state.SignInCommand.CanExecute(null));

            await state.SignInCommand.ExecuteAsync(null);
            auth.Verify(a => a.SignInAsync(It.IsAny<CancellationToken>()), Times.Once);

            auth.Setup(a => a.IsAuthenticated).Returns(true);
            state.IsAuthenticated = true;
            Assert.IsFalse(state.SignInCommand.CanExecute(null));
        });
    }

    [TestMethod]
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

    [TestMethod]
    public void MainWindow_Close_HidesInsteadOfClosing()
    {
        RunOnUi(() =>
        {
            var state = CreateAppState();
            var window = new MainWindow(state);
            window.Show();
            window.Close();
            Assert.IsFalse(window.IsVisible);
            window.Show();
            Assert.IsTrue(window.IsVisible);
        });
    }

    [TestMethod]
    public void AppState_LoadAvatar_InvalidUrl_ClearsImage()
    {
        RunOnUi(() =>
        {
            var auth = DaemonTestDoubles.CreateAuthService(authenticated: true);
            auth.Setup(a => a.AvatarUrl).Returns("http://127.0.0.1:9/avatar.png");
            var state = CreateAppState(auth.Object);
            Assert.IsNull(state.AvatarImage);
        });
    }

    [TestMethod]
    public void TrayMenu_ShowMainWindow_DoesNotThrow()
    {
        RunOnUi(() =>
        {
            var state = CreateAppState();
            var window = new MainWindow(state);
            var tray = new TrayMenu(state, window);
            tray.ShowMainWindow();
        });
    }

    [TestMethod]
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
                preferences.AutoStartEnabled = true;
                if (Environment.ProcessPath is not null)
                    Assert.IsTrue(AutoStart.IsEnabled);
                preferences.AutoStartEnabled = false;
                Assert.IsFalse(AutoStart.IsEnabled);
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

    [TestMethod]
    public void AppState_NotAuthenticated_ShowsSignedOutGatewayStatus()
    {
        RunOnUi(() =>
        {
            var auth = DaemonTestDoubles.CreateAuthService(authenticated: false);
            var state = CreateAppState(auth.Object);
            Assert.AreEqual("Not signed in", state.GatewayStatus);
        });
    }

    [TestMethod]
    public void UiDispatch_PostAndSend_ExecuteOnUiThread()
    {
        RunOnUi(() =>
        {
            var executed = false;
            UiDispatch.Post(() => executed = true);
            Assert.IsTrue(executed);

            UiDispatch.Send(() => executed = false);
            Assert.IsFalse(executed);
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
