using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using DevTools.Daemon.Composition;
using DevTools.Daemon.Desktop;
using DevTools.Daemon.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DevTools.Daemon;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        return args.Contains(AppConstants.StdioArg, StringComparer.OrdinalIgnoreCase) 
            ? RunStdioAsync(args).GetAwaiter().GetResult() 
            : RunDesktop();
    }

    private static int RunDesktop()
    {
        using var singleInstance = new SingleInstance();
        if (!singleInstance.IsFirstInstance)
            return 0;

        IHost? host = null;
        TrayMenu? tray = null;

        try
        {
            Application
                .Create()
                .UseWin32()
                .UseDirect2D()
                .UseTheme(ThemeVariant.System)
                .WithShutdownMode(ShutdownMode.OnExplicitShutdown)
                .OnStartup(() =>
                {
                    try
                    {
                        host = ServerHostBuilder.CreateDesktop();
                        tray = host.Services.GetRequiredService<TrayMenu>();
                        tray.Start();
                        StartHost(host);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Prompt(new MessageBoxOptions
                        {
                            Message = ex.ToString(),
                            Title = AppConstants.StartupErrorTitle,
                            Icon = PromptIconKind.Error,
                        });
                        Application.Shutdown();
                    }
                })
                .Run();
        }
        finally
        {
            try
            {
                if (host is not null)
                {
                    host.StopAsync(TimeSpan.FromSeconds(AppConstants.ShutdownTimeoutSeconds))
                        .GetAwaiter()
                        .GetResult();
                    host.Dispose();
                }
            }
            catch
            {
                /* best-effort */
            }

            tray?.Dispose();
        }

        return 0;
    }

    private static async void StartHost(IHost host)
    {
        try
        {
            await host.StartAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await MessageBox.PromptAsync(new MessageBoxOptions
            {
                Message = ex.ToString(),
                Title = AppConstants.StartupErrorTitle,
                Icon = PromptIconKind.Error,
            }).ConfigureAwait(true);
            Application.Shutdown();
        }
    }

    private static async Task<int> RunStdioAsync(string[] args)
    {
        try
        {
            using var host = ServerHostBuilder.CreateStdioHost(args);
            await host.RunAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
