using DevTools.Daemon.Control;
using DevTools.Daemon.Desktop.Updates;
using DevTools.Daemon.Desktop.Views;
using DevTools.Ipc;
using DevTools.Update.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
// ReSharper disable AsyncVoidEventHandlerMethod

namespace DevTools.Daemon;

public partial class App
{
    private IHost? _host;
    private SingleInstanceGuard? _singleInstance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var background = e.Args.Contains(IpcConstants.DaemonArgs.Background, StringComparer.OrdinalIgnoreCase);
            _singleInstance = new SingleInstanceGuard();
            if (!_singleInstance.IsFirstInstance)
            {
                if (!background) SingleInstanceGuard.RequestShow();
                _singleInstance.Dispose();
                _singleInstance = null;
                Shutdown();
                return;
            }

            _host = AppHostBuilder.CreateDesktop();
            var tray = _host.Services.GetRequiredService<TrayMenu>();
            tray.Start();
            _singleInstance.OnShowRequested(tray.ShowMainWindow);

            await _host.StartAsync();
            if (e.Args.Contains(IpcConstants.DaemonArgs.ShowUpdates, StringComparer.OrdinalIgnoreCase)) tray.ShowUpdatesTab(check: false);
            else if (!background) tray.ShowMainWindow();
            _host.Services.GetRequiredService<UpdateTray>().ShowSetupIfNotConfigured(_host.Services.GetRequiredService<DaemonUpdater>());
        }
        catch (Exception ex)
        {
            HandyControl.Controls.MessageBox.Show(
                ex.ToString(),
                AppConstants.StartupErrorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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
