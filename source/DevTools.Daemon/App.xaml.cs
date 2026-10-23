using DevTools.Daemon.Control;
using DevTools.Daemon.Desktop.Views;
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
            _singleInstance = new SingleInstanceGuard();
            if (!_singleInstance.IsFirstInstance)
            {
                _singleInstance.Dispose();
                _singleInstance = null;
                Shutdown();
                return;
            }

            _host = AppHostBuilder.CreateDesktop();
            _host.Services.GetRequiredService<TrayMenu>().Start();

            await _host.StartAsync();
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
