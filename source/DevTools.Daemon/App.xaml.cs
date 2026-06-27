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
            if (e.Args.Contains(DaemonConstants.StdioArg, StringComparer.OrdinalIgnoreCase))
            {
                await RunStdioHostAsync();
                Shutdown();
                return;
            }

            var singleInstance = new SingleInstance();
            if (!singleInstance.IsFirstInstance)
            {
                singleInstance.Dispose();
                Shutdown();
                return;
            }

            _host = DaemonHostBuilder.CreateTrayHost(singleInstance);
            await _host.StartAsync();

            var trayIcon = (TaskbarIcon)FindResource(DaemonConstants.TrayIconResourceKey)!;
            trayIcon.ForceCreate();
            trayIcon.DataContext = _host.Services.GetRequiredService<TrayViewModel>();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), DaemonConstants.StartupErrorTitle,
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
                await _host.StopAsync(TimeSpan.FromSeconds(DaemonConstants.ShutdownTimeoutSeconds));
                _host.Dispose();
            }
        }
        catch
        {
             /* best-effort */
        }

        base.OnExit(e);
    }

    private static async Task RunStdioHostAsync()
    {
        using var host = DaemonHostBuilder.CreateStdioHost();
        await host.RunAsync();
    }
}
