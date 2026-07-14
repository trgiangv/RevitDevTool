using DevTools.Daemon.Hosting;
using Microsoft.Extensions.Hosting;

namespace DevTools.Daemon;

internal static class Program
{
    [STAThread]
    public static async Task<int> Main(string[] args)
    {
        if (args.Contains(DaemonConstants.StdioArg, StringComparer.OrdinalIgnoreCase))
            return await RunStdioAsync(args).ConfigureAwait(false);

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }

    private static async Task<int> RunStdioAsync(string[] args)
    {
        try
        {
            using var host = DaemonHostBuilder.CreateStdioHost(args);
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
