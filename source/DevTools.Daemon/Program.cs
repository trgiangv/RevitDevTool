using DevTools.Daemon.Composition;
using DevTools.Daemon.Desktop;
using Microsoft.Extensions.Hosting;

namespace DevTools.Daemon;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains(AppConstants.StdioArg, StringComparer.OrdinalIgnoreCase))
            return RunStdioAsync(args).GetAwaiter().GetResult();

        var app = new App();
        app.InitializeComponent();
        return app.Run();
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
