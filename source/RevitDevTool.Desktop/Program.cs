using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Desktop.Services;
using RevitDevTool.Desktop.ViewModels;

namespace RevitDevTool.Desktop;

class Program
{
    public static IHost AppHost { get; private set; } = null!;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppHost = BuildHost(args);
        AppHost.Start();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            AppHost.StopAsync().GetAwaiter().GetResult();
            AppHost.Dispose();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static IHost BuildHost(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureServices(services =>
            {
                services.AddSingleton<IBatchExecutionService, BatchExecutionService>();

                // Page ViewModels
                services.AddSingleton<ProcessorPageViewModel>();
                services.AddSingleton<AssistantPageViewModel>();
                services.AddSingleton<DataPageViewModel>();
                services.AddSingleton<SettingsPageViewModel>();

                // Main ViewModel depends on page VMs
                services.AddSingleton<MainWindowViewModel>();

                // Window
                services.AddTransient<MainWindow>();
            })
            .Build();
    }
}