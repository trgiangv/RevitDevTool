using System.Windows;
using DevTools.Daemon.Hosting;
using DevTools.Daemon.Tray;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
// ReSharper disable AsyncVoidEventHandlerMethod

namespace DevTools.Daemon;

public partial class App
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var singleInstance = new SingleInstance();
            if (!singleInstance.IsFirstInstance)
            {
                singleInstance.Dispose();
                Shutdown();
                return;
            }

            _host = DaemonHostBuilder.CreateTrayHost(singleInstance);
            await _host.StartAsync();

            var trayIcon = (TaskbarIcon)FindResource(TrayUiConstants.TrayIconResourceKey)!;
            trayIcon.ForceCreate();
            trayIcon.DataContext = _host.Services.GetRequiredService<TrayViewModel>();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), TrayUiConstants.StartupErrorTitle,
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
                await _host.StopAsync(TimeSpan.FromSeconds(TrayUiConstants.ShutdownTimeoutSeconds));
                _host.Dispose();
            }
        }
        catch
        {
             /* best-effort */
        }

        base.OnExit(e);
    }
}
