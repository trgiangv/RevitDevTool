using System.Windows;
using DevTools.Daemon.Composition;
using DevTools.Daemon.Desktop;
using DevTools.Daemon.Views;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
// ReSharper disable AsyncVoidEventHandlerMethod

namespace DevTools.Daemon;

public partial class App
{
    private IHost? _host;
    private SingleInstance? _singleInstance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _singleInstance = new SingleInstance();
            if (!_singleInstance.IsFirstInstance)
            {
                _singleInstance.Dispose();
                _singleInstance = null;
                Shutdown();
                return;
            }

            _host = ServerHostBuilder.CreateDesktop();

            // Tray icon must be created on the WPF STA thread before any await.
            var trayIcon = (TaskbarIcon)FindResource(AppConstants.TrayIconResourceKey)!;
            trayIcon.ForceCreate();
            trayIcon.DataContext = _host.Services.GetRequiredService<TrayMenu>();

            await _host.StartAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), AppConstants.StartupErrorTitle,
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host is not null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(AppConstants.ShutdownTimeoutSeconds));
                _host.Dispose();
            }
        }
        catch
        {
            /* best-effort */
        }

        try
        {
            _singleInstance?.Dispose();
        }
        catch
        {
            /* best-effort */
        }

        base.OnExit(e);
    }
}
