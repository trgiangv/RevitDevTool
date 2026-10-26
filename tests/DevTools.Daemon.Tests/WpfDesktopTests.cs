using DevTools.Daemon.Auth;
using DevTools.Daemon.Control;
using DevTools.Daemon.Desktop.Helpers;
using DevTools.Daemon.Desktop.Models;
using DevTools.Daemon.Desktop.ViewModels;
using DevTools.Daemon.Desktop.Views;
using DevTools.Daemon.Tests.Support;
using DevTools.Daemon.Tunnel;
using DevTools.Settings.Configs;
using Moq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
            var state = CreateShellViewModel();
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
    public void ShellViewModel_CreateAvatar_DecodesImageBytes()
    {
        RunOnUi(() =>
        {
            var png = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
            var image = OverviewViewModel.CreateAvatar(png);
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

            var state = CreateShellViewModel();
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
    public void PreferencesViewModel_ThemeChange_PersistsSettings()
    {
        RunOnUi(() =>
        {
            var settingsPath = UserSettings.FilePath;
            var backup = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
            try
            {
                var store = DaemonTestDoubles.CreateUserSettingsStore(new UserSettings { Theme = AppTheme.Light });
                var preferences = new SettingsViewModel(store);

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
    public void ShellViewModel_RefreshAuthAndTunnelStatusText()
    {
        RunOnUi(() =>
        {
            var auth = DaemonTestDoubles.CreateAuthService(authenticated: true);
            var tunnel = DaemonTestDoubles.CreateTunnelStatus(TunnelStatus.Connected);
            var state = CreateShellViewModel(auth.Object, tunnel: tunnel.Object);

            Assert.IsTrue(state.Overview.IsAuthenticated);
            Assert.AreEqual("Connected", state.Overview.TunnelStatus);
            Assert.AreEqual("Test User", state.Overview.DisplayName);

            tunnel.Raise(t => t.StatusChanged += null!, new object(), new TunnelStatusChangedArgs(TunnelStatus.Reconnecting));
            Assert.AreEqual("Reconnecting...", state.Overview.TunnelStatus);

            state.SelectedTabIndex = 1;
            state.SelectedTabIndex = 2;
        });
    }

    [TestMethod]
    public void ShellViewModel_SignInCommand_CallsAuthService()
    {
        var auth = DaemonTestDoubles.CreateAuthService();
        RunOnUiAsync(async () =>
        {
            var state = CreateShellViewModel(auth.Object);
            Assert.IsTrue(state.Overview.SignInCommand.CanExecute(null));

            state.Overview.SignInCommand.Execute(null);
            auth.Verify(a => a.SignInAsync(It.IsAny<CancellationToken>()), Times.Once);

            auth.Setup(a => a.IsAuthenticated).Returns(true);
            state.Overview.IsAuthenticated = true;
            Assert.IsFalse(state.Overview.SignInCommand.CanExecute(null));
        });
    }

    [TestMethod]
    public void ShellViewModel_SignInCommand_RemainsEnabledWhileSignInIsPending()
    {
        var signInResult = new TaskCompletionSource<AuthResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var auth = DaemonTestDoubles.CreateAuthService();
        auth.Setup(a => a.SignInAsync(It.IsAny<CancellationToken>())).Returns(signInResult.Task);

        try
        {
            RunOnUi(() =>
            {
                var state = CreateShellViewModel(auth.Object);
                state.Overview.SignInCommand.Execute(null);

                Assert.IsTrue(state.Overview.SignInCommand.CanExecute(null));
            });
        }
        finally
        {
            signInResult.TrySetResult(new AuthResult(true));
        }
    }

    [TestMethod]
    public void ShellViewModel_SignOut_RefreshesState()
    {
        var auth = DaemonTestDoubles.CreateAuthService(authenticated: true);
        RunOnUiAsync(async () =>
        {
            var state = CreateShellViewModel(auth.Object);
            await state.Overview.SignOutAsync();
            auth.Verify(a => a.SignOutAsync(), Times.Once);
        });
    }

    [TestMethod]
    public void MainWindow_Close_HidesInsteadOfClosing()
    {
        RunOnUi(() =>
        {
            var state = CreateShellViewModel();
            var window = new MainWindow(state);
            window.Show();
            window.Close();
            Assert.IsFalse(window.IsVisible);
            window.Show();
            Assert.IsTrue(window.IsVisible);
        });
    }

    [TestMethod]
    public void ShellViewModel_LoadAvatar_InvalidUrl_ClearsImage()
    {
        RunOnUi(() =>
        {
            var auth = DaemonTestDoubles.CreateAuthService(authenticated: true);
            auth.Setup(a => a.AvatarUrl).Returns("http://127.0.0.1:9/avatar.png");
            var state = CreateShellViewModel(auth.Object);
            Assert.IsNull(state.Overview.AvatarImage);
        });
    }

    [TestMethod]
    public void TrayMenu_ShowMainWindow_DoesNotThrow()
    {
        RunOnUi(() =>
        {
            var state = CreateShellViewModel();
            var window = new MainWindow(state);
            var tray = new TrayMenu(state, window);
            tray.ShowMainWindow();
        });
    }

    [TestMethod]
    public void PreferencesViewModel_AutoStartToggle_UpdatesRegistry()
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
                var preferences = new SettingsViewModel(store);
                preferences.AutoStartEnabled = true;
                if (Environment.ProcessPath is not null)
                    Assert.IsTrue(StartupRegistration.IsEnabled);
                preferences.AutoStartEnabled = false;
                Assert.IsFalse(StartupRegistration.IsEnabled);
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
    public void ShellViewModel_NotAuthenticated_ShowsSignedOutTunnelStatusText()
    {
        RunOnUi(() =>
        {
            var auth = DaemonTestDoubles.CreateAuthService(authenticated: false);
            var state = CreateShellViewModel(auth.Object);
            Assert.AreEqual("Not signed in", state.Overview.TunnelStatus);
        });
    }

    [TestMethod]
    public void UiDispatch_PostAndSend_ExecuteOnUiThread()
    {
        RunOnUi(() =>
        {
            var executed = false;
            UiDispatchHelper.Post(() => executed = true);
            Assert.IsTrue(executed);

            UiDispatchHelper.Send(() => executed = false);
            Assert.IsFalse(executed);
        });
    }

    private static ShellViewModel CreateShellViewModel(
        IAuthService? auth = null,
        ITunnelStatusProvider? tunnel = null)
    {
        auth ??= DaemonTestDoubles.CreateAuthService().Object;
        tunnel ??= DaemonTestDoubles.CreateTunnelStatus().Object;
        var broker = DaemonTestDoubles.CreateHostConnections().Object;
        var scanner = DaemonTestDoubles.CreatePipeScanner().Object;
        var store = DaemonTestDoubles.CreateUserSettingsStore();
        return new ShellViewModel(auth, broker, scanner, DaemonTestDoubles.CreateUpdates(), store, tunnel);
    }
}
